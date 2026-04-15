using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using App4.Core;
using App4.Core.Protos;

namespace MyFullTrustConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("FullTrust Client initialized. Searching for UWP Sandbox Pipes...");
            
            string fullTrustToUwpPath = "";
            string uwpToFullTrustPath = "";
            
            while(string.IsNullOrEmpty(fullTrustToUwpPath) || string.IsNullOrEmpty(uwpToFullTrustPath))
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
                
                var reply = new TpmCommand { 
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
    }
}
