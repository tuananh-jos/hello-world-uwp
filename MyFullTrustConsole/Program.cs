using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App4.Core;
using App4.Core.Protos;
using MyFullTrustConsole.Services;

namespace MyFullTrustConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Manifest: BrowserExtensionGroup → /browser_extension, TpmGroup → /tpm
            if (args.Contains("/tpm"))
                await RunTpmModeAsync();
            else if (args.Contains("/browser_extension"))
                await RunLegacyModeAsync();
            else
                Console.WriteLine($"Unknown argument: {string.Join(" ", args)}");
        }

        // ─── Legacy flow (unchanged) ────────────────────────────────────────────
        static async Task RunLegacyModeAsync()
        {
            Console.WriteLine("FullTrust Client initialized. Searching for UWP Sandbox Pipes...");

            string fullTrustToUwpPath = "";
            string uwpToFullTrustPath = "";

            while (string.IsNullOrEmpty(fullTrustToUwpPath) || string.IsNullOrEmpty(uwpToFullTrustPath))
            {
                Console.WriteLine("Scanning system pipe paths...");
                string[] allPipes = Directory.GetFiles(@"\\.\pipe\");

                var pipe1 = allPipes.FirstOrDefault(p => p.EndsWith("App4FulltrustToUWP", StringComparison.OrdinalIgnoreCase));
                var pipe2 = allPipes.FirstOrDefault(p => p.EndsWith("App4UWPToFulltrust", StringComparison.OrdinalIgnoreCase));

                if (pipe1 != null && pipe2 != null)
                {
                    fullTrustToUwpPath = pipe1.Replace(@"\\.\pipe\", "");
                    uwpToFullTrustPath = pipe2.Replace(@"\\.\pipe\", "");
                    Console.WriteLine("\n[+] BINGO! Found hidden UWP pipes.");
                }
                else
                {
                    await Task.Delay(1000);
                }
            }

            var client = new PipeClient(inputPipeName: uwpToFullTrustPath, outputPipeName: fullTrustToUwpPath);
            client.OnDataReceived += (s, receivedMsg) =>
            {
                Console.WriteLine($"[UWP Payload]: Action: {receivedMsg.ActionName}, Sender: {receivedMsg.SenderInfo}");

                var reply = new TpmCommand
                {
                    ActionName = "ACK_OK_TPM_GIVEN",
                    SenderInfo = "Desktop_FullTrust_Lord",
                    TimestampTicks = (int)DateTime.Now.Ticks
                };
                client.SendMessage(reply);
            };

            Console.WriteLine("Connecting through the Sandbox barrier...");
            await client.ConnectAsync(999999);
            Console.WriteLine("🔥 Connected! Standing by for Protobuf objects...\n");

            await Task.Delay(-1);
        }

        // ─── TPM flow ────────────────────────────────────────────────────────────
        static async Task RunTpmModeAsync()
        {
            Console.WriteLine("[TPM Mode] Searching for TPM pipe channel...");

            string tpmUwpToFtPath = "";
            string tpmFtToUwpPath = "";

            while (string.IsNullOrEmpty(tpmUwpToFtPath) || string.IsNullOrEmpty(tpmFtToUwpPath))
            {
                Console.WriteLine("Scanning system pipe paths...");
                string[] allPipes = Directory.GetFiles(@"\\.\pipe\");

                var pipe1 = allPipes.FirstOrDefault(p => p.EndsWith("App4TpmFulltrustToUWP", StringComparison.OrdinalIgnoreCase));
                var pipe2 = allPipes.FirstOrDefault(p => p.EndsWith("App4TpmUWPToFulltrust", StringComparison.OrdinalIgnoreCase));

                if (pipe1 != null && pipe2 != null)
                {
                    tpmFtToUwpPath = pipe1.Replace(@"\\.\pipe\", "");
                    tpmUwpToFtPath = pipe2.Replace(@"\\.\pipe\", "");
                    Console.WriteLine("\n[+] Found TPM pipes.");
                }
                else
                {
                    await Task.Delay(1000);
                }
            }

            // inputPipeName = pipe we READ from (UWP→FT), outputPipeName = pipe we WRITE to (FT→UWP)
            var client = new TpmPipeClient(inputPipeName: tpmUwpToFtPath, outputPipeName: tpmFtToUwpPath);
            client.OnDataReceived += async (s, msg) =>
            {
                if (msg.PayloadCase != TpmPipeMessage.PayloadOneofCase.Command) return;

                var cmd = msg.Command;
                Console.WriteLine($"[TPM Pipe] Received: Action={cmd.ActionName}, Sender={cmd.SenderInfo}");

                if (cmd.ActionName == "GET_TPM_KEY")
                {
                    TpmResponse tpmResp = await QueryTpmAsync();
                    Console.WriteLine($"[TPM Pipe] Sending response: Status={tpmResp.Status}");
                    client.SendMessage(new TpmPipeMessage { Response = tpmResp });
                }
            };

            Console.WriteLine("[TPM Mode] Connecting...");
            await client.ConnectAsync(999999);
            Console.WriteLine("[TPM Mode] 🔥 Connected! Waiting for TPM commands...\n");

            await Task.Delay(-1);
        }

        static async Task<TpmResponse> QueryTpmAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var (ekPub, thumbprint) = NcryptService.GetEk();

                    Console.WriteLine($"[TPM] EK Thumbprint: {thumbprint[..16]}...");

                    return new TpmResponse
                    {
                        Status         = "OK",
                        KeyName        = thumbprint,
                        KeyAlgorithm   = ekPub.Length > 0 ? $"EK blob {ekPub.Length} bytes" : "Unknown",
                        TimestampTicks = DateTime.UtcNow.Ticks
                    };
                }
                catch (Exception ex)
                {
                    return new TpmResponse
                    {
                        Status         = "ERROR",
                        ErrorMessage   = ex.Message,
                        TimestampTicks = DateTime.UtcNow.Ticks
                    };
                }
            });
        }
    }
}
