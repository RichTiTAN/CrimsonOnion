using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class CustomBridgeDialog : Window
    {
        private readonly AppConfig _config;
        private readonly Action<string> _showToast;
        private bool _fetchingBridges = false;
        private string _moatBridgeType = "";
        private string _moatChallengeId = "";
        private string _moatChallengeStr = "";
        private int _moatIndex = 0;
        private HttpClient? _httpClient;
        private CancellationTokenSource? _cts;

        private readonly string[] _moatEndpoints = {
            "https://bridges.torproject.org/moat",
            "https://bridges2.torproject.org/moat",
            "https://tor.eff.org/moat"
        };

        public CustomBridgeDialog(AppConfig config, Action<string> showToast)
        {
            _config = config;
            _showToast = showToast;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Loaded += OnLoaded;
            Closing += (s, e) => CancelFetch();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            txtInput.Text = _config.CustomBridgeLine;
            txtInput.Focus();
            txtInput.CaretIndex = txtInput.Text.Length;

            btnOk.Click += OnSave;
            btnCancel.Click += (s, ev) => { DialogResult = false; Close(); };
            btnCloseDialog.Click += (s, ev) => { DialogResult = false; Close(); };
            btnGetWebTunnel.Click += (s, ev) => StartFetch("webtunnel");
            btnGetObfs4.Click += (s, ev) => StartFetch("obfs4");
            btnCaptchaSubmit.Click += (s, ev) => _ = SubmitCaptchaAsync();
            btnCaptchaCancel.Click += (s, ev) => CancelFetch();
            txtCaptchaSol.KeyDown += (s, ev) =>
            {
                if (ev.Key == System.Windows.Input.Key.Enter && btnCaptchaSubmit.IsEnabled)
                    _ = SubmitCaptchaAsync();
            };
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var input = txtInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                // Show inline warning
                _showToast("Custom bridge cannot be empty.");
                return;
            }
            if (_fetchingBridges) CancelFetch();
            _config.CustomBridgeLine = input;
            DialogResult = true;
            Close();
        }

        private void StartFetch(string bridgeType)
        {
            if (_fetchingBridges) { CancelFetch(); return; }
            _fetchingBridges = true;
            _moatBridgeType = bridgeType;
            _moatIndex = 0;

            if (bridgeType == "webtunnel")
            {
                btnGetWebTunnel.Content = "FETCHING...";
                btnGetObfs4.IsEnabled = false;
            }
            else
            {
                btnGetObfs4.Content = "FETCHING...";
                btnGetWebTunnel.IsEnabled = false;
            }

            // Create the client for this fetch operation
            _httpClient?.Dispose();
            try
            {
                var sysProxy = WebRequest.GetSystemWebProxy();
                sysProxy.Credentials = CredentialCache.DefaultCredentials;
                _httpClient = new HttpClient(new HttpClientHandler { Proxy = sysProxy, UseProxy = true });
            }
            catch 
            { 
                _httpClient = new HttpClient(); 
            }
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");

            _ = RequestChallengeAsync();
        }

        private void CancelFetch(bool keepClient = false)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _httpClient?.CancelPendingRequests();
            _httpClient?.Dispose();
            _httpClient = null;

            _fetchingBridges = false;


            Dispatcher.Invoke(() =>
            {
                btnGetWebTunnel.Content = "WEBTUNNEL";
                btnGetWebTunnel.IsEnabled = true;
                btnGetObfs4.Content = "OBFS4";
                btnGetObfs4.IsEnabled = true;
                btnOk.IsEnabled = true;
                btnCaptchaSubmit.Content = "SUBMIT";
                btnCaptchaSubmit.IsEnabled = true;
                panGetBridges.Visibility = Visibility.Visible;
                SetDialogHeight(false);
            });
        }

        private async Task RequestChallengeAsync()
        {
            for (_moatIndex = 0; _moatIndex < _moatEndpoints.Length; _moatIndex++)
            {
                if (!_fetchingBridges) return;

                await Dispatcher.InvokeAsync(() => btnCaptchaSubmit.Content = "FETCHING...");

                var url = _moatEndpoints[_moatIndex] + "/fetch";
                var body = JsonConvert.SerializeObject(new { data = new object[] { new { version = "0.1.0", type = "client-transports", supported = new[] { _moatBridgeType } } } });

                try
                {
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                    _httpClient!.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
                    var content = new StringContent(body, Encoding.UTF8, "application/vnd.api+json");
                    var response = await _httpClient.PostAsync(url, content, _cts.Token);
                    var resultStr = await response.Content.ReadAsStringAsync(_cts.Token);

                    if (!_fetchingBridges) return;

                    var res = JObject.Parse(resultStr);
                    if (res["data"] is JArray dataArr && dataArr.Count > 0 && dataArr[0] is JObject d0 &&
                        d0["id"] != null && d0["image"] != null && d0["challenge"] != null)
                    {
                        _moatChallengeId = d0["id"]!.ToString();
                        _moatChallengeStr = d0["challenge"]!.ToString();
                        var imgBytes = Convert.FromBase64String(d0["image"]!.ToString());

                        await Dispatcher.InvokeAsync(() =>
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = new MemoryStream(imgBytes);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            imgCaptcha.Source = bmp;
                            panGetBridges.Visibility = Visibility.Collapsed;
                            SetDialogHeight(true);
                            txtCaptchaSol.Text = "";
                            btnCaptchaSubmit.Content = "SUBMIT";
                            btnCaptchaSubmit.IsEnabled = true;
                            txtCaptchaSol.Focus();
                        });
                        return;
                    }
                }
                catch (Exception)
                {
                    if (!_fetchingBridges) return;
                }
            }

            await Dispatcher.InvokeAsync(() =>
            {
                CancelFetch();
                System.Windows.MessageBox.Show(
                    "Could not reach Tor Project servers.\nTry again later or check your network.\n\n" +
                    "Alternatively, get bridges manually:\n  - Telegram: @GetBridgesBot\n  - Email: bridges@torproject.org\n  - Browser: bridges.torproject.org",
                    "Fetch Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private async Task SubmitCaptchaAsync()
        {
            var solution = txtCaptchaSol.Text.Trim();
            if (string.IsNullOrWhiteSpace(solution)) return;

            btnCaptchaSubmit.Content = "VERIFYING...";
            btnCaptchaSubmit.IsEnabled = false;

            var url = _moatEndpoints[_moatIndex < _moatEndpoints.Length ? _moatIndex : 0] + "/check";
            var body = JsonConvert.SerializeObject(new
            {
                data = new object[]
                {
                    new
                    {
                        id = _moatChallengeId, version = "0.1.0", type = "moat-solution",
                        transport = _moatBridgeType, challenge = _moatChallengeStr,
                        solution = solution, qrcode = "false"
                    }
                }
            });

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var content = new StringContent(body, Encoding.UTF8, "application/vnd.api+json");
                var response = await _httpClient!.PostAsync(url, content, _cts.Token); 
                var resultStr = await response.Content.ReadAsStringAsync();

                if (!_fetchingBridges) return;

                var res = JObject.Parse(resultStr);
                var dataArr = res["data"] as JArray;
                if (dataArr != null && dataArr.Count > 0 && dataArr[0]["bridges"] is JArray bridges && bridges.Count > 0)
                {
                    var lines = string.Join("\n", bridges.Select(b => b.ToString()));
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var existing = txtInput.Text.Trim();
                        txtInput.Text = string.IsNullOrWhiteSpace(existing) ? lines : $"{existing}\n{lines}";
                        txtInput.CaretIndex = txtInput.Text.Length;
                        CancelFetch();
                    });
                }
                else
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        txtCaptchaSol.Text = "";
                        btnCaptchaSubmit.Content = "NEW CAPTCHA...";
                    });
                    await RequestChallengeAsync();
                }
            }
            catch
            {
                if (!_fetchingBridges) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    txtCaptchaSol.Text = "";
                    btnCaptchaSubmit.Content = "NEW CAPTCHA...";
                });
                await RequestChallengeAsync(); 
            }
        }

        private void SetDialogHeight(bool isCaptcha)
        {
            var targetH = isCaptcha ? 320.0 : 260.0;
            var tBorderH = isCaptcha ? 266.0 : 206.0;
            var tBtnTop = isCaptcha ? 220.0 : 165.0;

            var dur = new Duration(TimeSpan.FromMilliseconds(250));
            this.BeginAnimation(HeightProperty, new DoubleAnimation(targetH, dur));
            borderMain.BeginAnimation(HeightProperty, new DoubleAnimation(tBorderH, dur));

            Canvas.SetTop(btnOk, tBtnTop);
            Canvas.SetTop(btnCancel, tBtnTop);

            if (isCaptcha)
            {
                panCaptcha.Visibility = Visibility.Visible;
                panCaptcha.IsHitTestVisible = true;
                panCaptcha.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, dur));
            }
            else
            {
                panCaptcha.IsHitTestVisible = false;
                var fadeOut = new DoubleAnimation(0.0, dur);
                fadeOut.Completed += (s, e) =>
                {
                    if (panCaptcha.Opacity < 0.1) panCaptcha.Visibility = Visibility.Collapsed;
                };
                panCaptcha.BeginAnimation(OpacityProperty, fadeOut);
            }
        }
    }
}
