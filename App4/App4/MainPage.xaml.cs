using System;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;
using App4.Core;
using App4.Core.Protos;

namespace App4
{
    public sealed partial class MainPage : Page
    {
        private PipeServer _server;

        public MainPage()
        {
            InitializeComponent();
            
            // UWP requires LOCAL\ prefix to create Sandbox virtualized object
            _server = new PipeServer(@"LOCAL\App4FulltrustToUWP", @"LOCAL\App4UWPToFulltrust");
            _server.OnDataReceived += async (s, e) =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    TxtStatus.Text = $"Reply: Action '{e.ActionName}' from '{e.SenderInfo}' at tick {e.TimestampTicks}";
                });
            };
            _server.OnError += async (s, ex) => 
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    TxtStatus.Text = "Server Error: " + ex.GetType().Name + " - " + ex.Message;
                });
            };

            // Non-blocking start
            _ = _server.StartListeningAsync();
        }

        private async void LaunchServer_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        { 
            try
            {
                TxtStatus.Text = "Launching Desktop Client...";
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("WriteLogGroup");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Client Launch Error: " + ex.Message;
            }
        }

        private async void VerifyHello_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Prompting Windows Hello/PIN...";
                
                // Bước 1: Kiểm tra xem máy có hỗ trợ vân tay, FaceID hay mã PIN không
                var availability = await Windows.Security.Credentials.UI.UserConsentVerifier.CheckAvailabilityAsync();
                
                if (availability == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available)
                {
                    // Bước 2: Bật Popup xanh chính chủ của Windows lên đòi xác thực
                    var consentResult = await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync("Please verify your identity before extracting TPM.");
                    
                    if (consentResult == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified)
                    {
                        TxtStatus.Text = "Windows Hello: VERIFIED SUCCESSFULLY! ✓";
                    }
                    else
                    {
                        TxtStatus.Text = $"Windows Hello: Verification Failed ({consentResult}) ❌";
                    }
                }
                else
                {
                    TxtStatus.Text = $"Windows Hello is unavailable: {availability} ⚠️";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "System Error: " + ex.Message;
            }
        }

        private void SendTPM_Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var tpmReq = new TpmCommand 
                { 
                    ActionName = "GET_TPM_KEY", 
                    SenderInfo = "App4_UWP_Sandbox",
                    TimestampTicks = (int)DateTime.Now.Ticks
                };
                
                _server.SendMessage(tpmReq);
                TxtStatus.Text = "Binary payload dispatched! Standing by...";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "System Error: " + ex.Message;
            }
        }
    }
}
