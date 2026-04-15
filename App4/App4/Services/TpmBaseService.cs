using System.Runtime.InteropServices;

namespace App4.Services
{
    /// <summary>
    /// Wraps TBS (TPM Base Services) — tbs.dll P/Invoke.
    /// Không cần FullTrust, hoạt động trực tiếp từ UWP sandbox.
    /// </summary>
    public static class TpmBaseService
    {
        // ── P/Invoke ─────────────────────────────────────────────────────────

        [DllImport("tbs.dll")]
        private static extern uint Tbsi_GetDeviceInfo(uint size, out TpmDeviceInfo deviceInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct TpmDeviceInfo
        {
            public uint StructVersion;
            public uint TpmVersion;        // 1 = TPM 1.2 | 2 = TPM 2.0
            public uint TpmInterfaceType;  // xem TpmInterface enum bên dưới
            public uint AuthLevel;
        }

        // ── Enums ─────────────────────────────────────────────────────────────

        public enum TpmVersion { NotPresent = 0, V12 = 1, V20 = 2 }

        public enum TpmInterface
        {
            Unknown   = 0,
            Tis       = 1,  // TIS (TPM Interface Specification)
            TrustZone = 2,
            Hardware  = 3,  // FIFO/CRB hardware TPM
            Emulator  = 4,
            Spb       = 5,  // Serial Peripheral Bus (firmware TPM)
        }

        // ── Public result ─────────────────────────────────────────────────────

        public record TpmInfo(TpmVersion Version, TpmInterface Interface, string Summary);

        // ── Public API ────────────────────────────────────────────────────────

        public static TpmInfo GetDeviceInfo()
        {
            uint ret = Tbsi_GetDeviceInfo((uint)Marshal.SizeOf<TpmDeviceInfo>(), out var info);

            if (ret != 0)
                throw new ExternalException($"Tbsi_GetDeviceInfo failed: 0x{ret:X8}");

            var ver  = (TpmVersion)info.TpmVersion;
            var iface = (TpmInterface)info.TpmInterfaceType;

            string versionStr = ver switch
            {
                TpmVersion.V12 => "TPM 1.2",
                TpmVersion.V20 => "TPM 2.0",
                _              => "Not Present"
            };

            string ifaceStr = iface switch
            {
                TpmInterface.Hardware  => "Hardware (FIFO/CRB)",
                TpmInterface.Emulator  => "Emulator (fTPM/vTPM)",
                TpmInterface.Spb       => "Firmware (SPB)",
                TpmInterface.TrustZone => "TrustZone",
                TpmInterface.Tis       => "TIS",
                _                      => "Unknown"
            };

            return new TpmInfo(ver, iface, $"{versionStr} — {ifaceStr}");
        }
    }
}
