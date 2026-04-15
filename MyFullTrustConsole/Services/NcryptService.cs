using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MyFullTrustConsole.Services
{
    /// <summary>
    /// NCrypt P/Invoke wrapper — lấy EK (Endorsement Key) public key từ TPM KSP.
    /// Không cần NuGet nào thêm, thuần Win32/NCrypt.
    /// </summary>
    public static class NcryptService
    {
        // ── P/Invoke ─────────────────────────────────────────────────────────

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptOpenStorageProvider(out nint hProvider, string pszProviderName, uint dwFlags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptGetProperty(nint hObject, string pszProperty, byte[]? pbOutput, uint cbOutput, out uint pcbResult, uint dwFlags);

        [DllImport("ncrypt.dll")]
        private static extern int NCryptFreeObject(nint hObject);

        // ── Constants ─────────────────────────────────────────────────────────

        private const string TPM_PROVIDER       = "Microsoft Platform Crypto Provider";
        private const string EKPUB_PROPERTY     = "PCP_EKPub";   // NCRYPT_PCP_EKPUB_PROPERTY
        private const int    S_OK               = 0;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Lấy EK public key từ TPM, trả về:
        ///   - EkPublicKey : raw bytes của BCRYPT_RSAKEY_BLOB / BCRYPT_ECCKEY_BLOB
        ///   - Thumbprint  : SHA-256 của raw bytes, encode hex
        /// </summary>
        public static (byte[] EkPublicKey, string Thumbprint) GetEk()
        {
            nint hProv = 0;
            try
            {
                // 1. Mở TPM Key Storage Provider
                int hr = NCryptOpenStorageProvider(out hProv, TPM_PROVIDER, 0);
                if (hr != S_OK)
                    throw new CryptographicException($"NCryptOpenStorageProvider failed: 0x{hr:X8}");

                // 2. Query size của EK public key blob
                hr = NCryptGetProperty(hProv, EKPUB_PROPERTY, null, 0, out uint cbResult, 0);
                if (hr != S_OK || cbResult == 0)
                    throw new CryptographicException($"NCryptGetProperty (size query) failed: 0x{hr:X8}");

                // 3. Query thực sự — lấy BCRYPT_RSAKEY_BLOB hoặc BCRYPT_ECCKEY_BLOB
                byte[] ekPub = new byte[cbResult];
                hr = NCryptGetProperty(hProv, EKPUB_PROPERTY, ekPub, cbResult, out _, 0);
                if (hr != S_OK)
                    throw new CryptographicException($"NCryptGetProperty (data) failed: 0x{hr:X8}");

                // 4. Tính thumbprint = SHA-256 của raw blob
                string thumbprint = Convert.ToHexString(SHA256.HashData(ekPub));

                return (ekPub, thumbprint);
            }
            finally
            {
                if (hProv != 0) NCryptFreeObject(hProv);
            }
        }
    }
}
