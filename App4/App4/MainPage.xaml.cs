using System;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;
using App4.Core;
using App4.Core.Protos;
using App4.Services;

namespace App4
{
    public sealed partial class MainPage : Page
    {
        private PipeServer _server;
        private TpmPipeServer _tpmServer;

        public MainPage()
        {
            InitializeComponent();

            // ── Browser Extension pipe (legacy TpmCommand protocol) ─────────────
            _server = new PipeServer(@"LOCAL\App4FulltrustToUWP", @"LOCAL\App4UWPToFulltrust");
            _server.OnDataReceived += async (s, e) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    TxtStatus.Text = $"[BrowserExt] Reply: '{e.ActionName}' from '{e.SenderInfo}'";
                });
            };
            _server.OnError += async (s, ex) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    TxtStatus.Text = "[BrowserExt] Error: " + ex.Message;
                });
            };
            _ = _server.StartListeningAsync();

            // ── TPM pipe (TpmPipeMessage protocol) ──────────────────────────────
            _tpmServer = new TpmPipeServer(@"LOCAL\App4TpmFulltrustToUWP", @"LOCAL\App4TpmUWPToFulltrust");
            _tpmServer.OnDataReceived += async (s, msg) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    switch (msg.PayloadCase)
                    {
                        case TpmPipeMessage.PayloadOneofCase.Response:
                            var r = msg.Response;
                            if (r.Status == "OK")
                            {
                                TxtStatus.Text = string.IsNullOrEmpty(r.Manufacturer)
                                    ? $"Write OK\n{r.ErrorMessage}"
                                    : $"TPM OK\nManufacturer : {r.Manufacturer}\nSpec Version : {r.SpecVersion}\nKey Name     : {r.KeyName}\nAlgorithm    : {r.KeyAlgorithm}";
                            }
                            else
                            {
                                TxtStatus.Text = $"{r.Status}: {r.ErrorMessage}";
                            }
                            break;

                        case TpmPipeMessage.PayloadOneofCase.Command:
                            TxtStatus.Text = $"[TPM] Command: {msg.Command.ActionName}";
                            break;

                    }
                });
            };
            _tpmServer.OnError += async (s, ex) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    TxtStatus.Text = "[TPM] Error: " + ex.Message;
                });
            };
            _ = _tpmServer.StartListeningAsync();
        }

        // ── Browser Extension handlers ──────────────────────────────────────────

        private async void LaunchBrowserExt_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Launching Browser Extension process...";
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("BrowserExtensionGroup");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Launch Error: {ex.GetType().Name} 0x{ex.HResult:X8}\n{ex.Message}";
            }
        }

        private void SendBrowserExt_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                _server.SendMessage(new TpmCommand
                {
                    ActionName     = "GET_LOG",
                    SenderInfo     = "App4_UWP_Sandbox",
                    TimestampTicks = (int)DateTime.Now.Ticks
                });
                TxtStatus.Text = "[BrowserExt] Command dispatched!";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Error: " + ex.Message;
            }
        }

        // ── TPM handlers ────────────────────────────────────────────────────────

        private async void LaunchTpm_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Launching TPM process...";
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("TpmGroup");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Launch Error: {ex.GetType().Name} 0x{ex.HResult:X8}\n{ex.Message}";
            }
        }

        private void SendTpm_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                _tpmServer.SendMessage(new TpmPipeMessage
                {
                    Command = new TpmCommand
                    {
                        ActionName     = "GET_TPM_KEY",
                        SenderInfo     = "App4_UWP_Sandbox",
                        TimestampTicks = (int)DateTime.Now.Ticks
                    }
                });
                TxtStatus.Text = "TPM query dispatched! Standing by...";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Error: " + ex.Message;
            }
        }

        // ── TPM Version (local, no pipe) ─────────────────────────────────────────

        private void CheckTpmVersion_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var info = TpmBaseService.GetDeviceInfo();
                TxtStatus.Text = $"TPM Version Check\n{info.Summary}";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "TPM Check Error: " + ex.Message;
            }
        }

        // ── Write Test handlers ──────────────────────────────────────────────────

        private async void TestUwpWrite_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync("App4UwpTest.txt",
                    Windows.Storage.CreationCollisionOption.ReplaceExisting);
                await Windows.Storage.FileIO.WriteTextAsync(file,
                    $"Written by UWP at {DateTime.Now}");
                TxtStatus.Text = $"UWP Write OK\n{file.Path}";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "UWP Write Error: " + ex.Message;
            }
        }

        private void TestFullTrustWrite_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                _tpmServer.SendMessage(new TpmPipeMessage
                {
                    Command = new TpmCommand { ActionName = "WRITE_FULLTRUST_FILE", SenderInfo = "App4_UWP", TimestampTicks = (int)DateTime.Now.Ticks }
                });
                TxtStatus.Text = "FullTrust Write dispatched...";
            }
            catch (Exception ex) { TxtStatus.Text = "Error: " + ex.Message; }
        }

        private void TestAdminWrite_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                _tpmServer.SendMessage(new TpmPipeMessage
                {
                    Command = new TpmCommand { ActionName = "WRITE_ADMIN_FILE", SenderInfo = "App4_UWP", TimestampTicks = (int)DateTime.Now.Ticks }
                });
                TxtStatus.Text = "Admin Write dispatched — UAC prompt incoming...";
            }
            catch (Exception ex) { TxtStatus.Text = "Error: " + ex.Message; }
        }

        // ── Windows Hello ────────────────────────────────────────────────────────

        private async void VerifyHello_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Prompting Windows Hello/PIN...";

                var availability = await Windows.Security.Credentials.UI.UserConsentVerifier.CheckAvailabilityAsync();

                if (availability == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available)
                {
                    var result = await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync(
                        "Please verify your identity before extracting TPM.");

                    TxtStatus.Text = result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified
                        ? "Windows Hello: VERIFIED SUCCESSFULLY! ✓"
                        : $"Windows Hello: Failed ({result}) ❌";
                }
                else
                {
                    TxtStatus.Text = $"Windows Hello unavailable: {availability} ⚠️";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Error: " + ex.Message;
            }
        }
    }
}
