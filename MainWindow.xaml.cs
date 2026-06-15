using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CrimsonOnion.Dialogs;
using CrimsonOnion.Models;
using CrimsonOnion.Services;

namespace CrimsonOnion
{
    public partial class MainWindow : Window
    {
        // ── Core state 
        private readonly AppConfig _cfg = new();
        private readonly AppState _state = new();
        private const string AppVersion = "1.1.0";

        // ── Paths 
        private string GetAppPath(string rel) => Path.Combine(_cfg.BaseDir, rel);

        // ── Process PIDs 
        private int?[] _torPids = new int?[8];
        private int? _xrayDebugPid;
        private int? _sbDebugPid;
        private int? _xrayDohPid;

        // ── Polled values 
        private int _pollSelCount;
        private string _pollMode = "Proxy Mode";
        private string _pollSelBridge = "meek_lite";

        // ── Timers 
        private DispatcherTimer? _sessionClockTimer;
        private DispatcherTimer? _statsTimer;
        private DispatcherTimer? _bootstrapTimer;
        private DispatcherTimer? _logTimer;
        private DispatcherTimer? _logClearTimer;
        private DispatcherTimer? _saveDebounceTimer;
        private DispatcherTimer? _xrayRestartTimer;
        private DispatcherTimer? _pingTimer;
        private DispatcherTimer? _hideAdvTimer;
        private DispatcherTimer? _hideLogTimer;
        private DispatcherTimer? _staggerTimer;
        private List<int> _staggerQueue = new();

        private CancellationTokenSource? _geoCts;
        private CancellationTokenSource? _updateCts;

        // ── System tray 
        private NotifyIcon? _trayIcon;

        // ── Brushes 
        private readonly SolidColorBrush BrGreen       = new(System.Windows.Media.Color.FromRgb(0x68, 0xD3, 0x91));
        private readonly SolidColorBrush BrGray        = new(System.Windows.Media.Color.FromRgb(0xA0, 0xAE, 0xC0));
        private readonly SolidColorBrush BrRed         = new(System.Windows.Media.Color.FromRgb(0xE5, 0x3E, 0x3E));
        private readonly SolidColorBrush BrWhite       = new(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
        private readonly SolidColorBrush BrDarkRed     = new(System.Windows.Media.Color.FromRgb(0x8B, 0x4A, 0x4A));
        private readonly SolidColorBrush BrDarkGray    = new(System.Windows.Media.Color.FromRgb(0x4A, 0x55, 0x68));
        private readonly SolidColorBrush BrOrange      = new(System.Windows.Media.Color.FromRgb(0xF6, 0xAD, 0x55));
        private readonly SolidColorBrush BrActiveRoute = new(System.Windows.Media.Color.FromArgb(0x80, 0x64, 0x6B, 0x75));

        // ── Logs tracking 
        private long _lastXrayLogPos = 0;
        private List<string> _xrayLogLines = new();

        private static readonly HttpClient _xrayGrpcClient = new HttpClient(new SocketsHttpHandler { EnableMultipleHttp2Connections = true }) 
        { 
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        private static readonly HttpClient _geoPingClient = new HttpClient(new HttpClientHandler
        {
            Proxy = new WebProxy("http://127.0.0.1:10818"),
            UseProxy = true
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };


        // ────────────────────────────────────────────────────────────────────────
        public MainWindow()
        {
            // Resolve base dir from executing assembly location
            _cfg.BaseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _cfg.CfgFile  = GetAppPath(@"Data\multiplexer_settings.json");
            _cfg.XrayDir  = GetAppPath(@"Data\Xray");
            _cfg.HaPath   = GetAppPath(@"Data\HAproxy");
            _cfg.SbDir    = GetAppPath(@"Data\sing_box");

            ConfigService.Load(_cfg, _state, _cfg.CfgFile);

            InitializeComponent();

            // Set app icon
            var iconPath = GetAppPath("icon.ico");
            if (File.Exists(iconPath))
                Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));

            lblTitleText.Text = $"CrimsonOnion v{AppVersion}";

            // Custom chrome
            MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is FrameworkElement fe &&
                    !(fe is System.Windows.Controls.Button || fe is System.Windows.Controls.Primitives.ButtonBase ||
                      fe is System.Windows.Controls.ComboBox || fe is System.Windows.Controls.TextBox || fe is System.Windows.Controls.ScrollViewer))
                {
                    try { DragMove(); } catch { /* Can throw if mouse is released prematurely */ }
                }
            };
            btnMinimize.Click += (s, e) => WindowState = WindowState.Minimized;
            btnClose.Click    += (s, e) => Close();

            // Clip geometry
            var clipGeom = new RectangleGeometry { RadiusX = 8, RadiusY = 8 };
            SizeChanged += (s, e) =>
            {
                if (borderClip != null)
                {
                    clipGeom.Rect = new Rect(0, 0, borderClip.ActualWidth, borderClip.ActualHeight);
                    borderClip.Clip = clipGeom;
                }
            };

            PopulateCombos();
            InitialColors();
            UpdateRoutingToggle();
            EvaluateProxyExclusivity();
            WireEvents();
            InitTimers();
            InitTrayIcon();
            ResetButtonText();

            if (!double.IsNaN(_cfg.WindowLeft) && !double.IsNaN(_cfg.WindowTop))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _cfg.WindowLeft;
                Top = _cfg.WindowTop;
            }
            else if (_cfg.StartMinimized)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = (SystemParameters.WorkArea.Width - Width) / 2 + SystemParameters.WorkArea.Left;
                Top = (SystemParameters.WorkArea.Height - Height) / 2 + SystemParameters.WorkArea.Top;
            }

            if (_cfg.StartMinimized)
            {
                WindowState = WindowState.Minimized;
            }
            
            Loaded += (s, e) =>
            {
                if (_cfg.StartMinimized && _cfg.MinimizeToTray)
                {
                    Hide();
                }
            };
        }

        // COMBO SETUP
        private void PopulateCombos()
        {
            // Bridge
            AddComboItem(comboBridge, "Direct (None)", "Direct (None)");
            AddComboItem(comboBridge, "meek_lite",     "meek_lite");
            AddComboItem(comboBridge, "obfs4",         "obfs4");
            AddComboItem(comboBridge, "snowflake",     "snowflake");
            AddComboItem(comboBridge, "Custom",        "Custom", isSpecial: true);

            // Config
            AddComboItem(comboConfig, "Optimized", "Optimized");
            AddComboItem(comboConfig, "Custom",    "Custom", isSpecial: true);
            AddComboItem(comboConfig, "Expert",    "Expert", isSpecial: true);

            // Count
            for (int n = 1; n <= 8; n++)
                AddComboItem(comboCount, n.ToString(), n.ToString());

            SetComboTag(comboBridge, _cfg.LastBridge);
            SetComboTag(comboConfig, _cfg.LastConfig);
            SetComboTag(comboCount,  _cfg.LastCount);

            _state.PreviousBridge = GetComboTag(comboBridge) ?? "meek_lite";
            _state.PreviousConfig = GetComboTag(comboConfig) ?? "Optimized";
        }

        private void AddComboItem(System.Windows.Controls.ComboBox combo, string text, string tag, bool isSpecial = false)
        {
            var item = new ComboBoxItem { Content = text, Tag = tag };
            if (isSpecial)
            {
                item.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    combo.IsDropDownOpen = false;
                    
                    _state.IgnoreComboChange = true;
                    combo.SelectedItem = item;
                    _state.IgnoreComboChange = false;

                    if (combo == comboBridge)
                    {
                        _state.IgnoreComboChange = true;
                        if (!ShowCustomBridgeDialog())
                            SetComboTag(combo, _state.PreviousBridge);
                        else
                        {
                            _state.PreviousBridge = "Custom";
                            _cfg.LastBridge = "Custom";
                            UpdateRoutingToggle();
                            if (_state.IsEngineRunning)
                                ShowToast("Please reconnect to apply the changes.");
                        }
                        RequestConfigSave();
                        _state.IgnoreComboChange = false;
                    }
                    else if (combo == comboConfig)
                    {
                        _state.IgnoreComboChange = true;
                        bool ok;
                        if (tag == "Custom")
                        {
                            ok = ShowExitNodeDialog();
                            if (!ok) SetComboTag(combo, _state.PreviousConfig);
                            else { _state.PreviousConfig = "Custom"; _cfg.LastConfig = "Custom"; }
                        }
                        else // Expert
                        {
                            ok = ShowExpertConfigDialog();
                            if (!ok) SetComboTag(combo, _state.PreviousConfig);
                            else { _state.PreviousConfig = "Expert"; _cfg.LastConfig = "Expert"; }
                        }
                        if (ok && _state.IsEngineRunning)
                            ShowToast("Please reconnect to apply the changes.");
                        RequestConfigSave();
                        _state.IgnoreComboChange = false;
                    }
                };
            }
            combo.Items.Add(item);
        }

        private void SetComboTag(System.Windows.Controls.ComboBox combo, string tag)
        {
            foreach (ComboBoxItem item in combo.Items)
                if ((string)item.Tag == tag) { combo.SelectedItem = item; return; }
        }

        private string? GetComboTag(System.Windows.Controls.ComboBox combo)
            => combo.SelectedItem is ComboBoxItem i ? (string)i.Tag : null;

        // INITIAL UI STATE
        private void InitialColors()
        {
            SetAutoConnectState(false, false);
            SetAdvState(_state.IsAdvancedOpen);
            SetToggle(btnV2rayTog,    _cfg.EnableV2rayChain);
            SetToggle(btnDirectTog,   _cfg.EnableDirect);
            SetToggle(btnOutboundTog, _cfg.EnableOutboundProxy);
            SetToggle(btnDohTog,      _cfg.EnableUpstreamDoh);
            SetToggle(btnBootTog,     _cfg.LaunchOnBoot);
            SetToggle(btnStartMinTog, _cfg.StartMinimized);
            SetToggle(btnDebugTog,    _cfg.DebugMode);
            SetToggle(btnLanTog,      _cfg.AllowLanConnections);
            SetToggle(btnTrayTog,     _cfg.MinimizeToTray);
            SetToggle(btnAdBlockTog,  _cfg.EnableAdBlock);
            SetLogsToggle(_state.IsLogsOpen);
        }

        private void SetToggle(System.Windows.Controls.Button btn, bool state, string on = "ENABLED", string off = "DISABLED")
        {
            btn.Background = System.Windows.Media.Brushes.Transparent;
            btn.Content    = state ? on.ToUpper() : off.ToUpper();
            btn.Foreground = state ? BrGreen : BrRed;
        }

        private void SetLogsToggle(bool state)
        {
            btnLogsTog.Background = System.Windows.Media.Brushes.Transparent;
            btnLogsTog.Foreground = BrGray;
            btnLogsTog.Content    = state ? "HIDE" : "SHOW";
        }

        private void SetAutoConnectState(bool state, bool animate)
        {
            var tpl    = btnAutoStartMain.Template;
            var t1     = tpl?.FindName("transAutoConnect", btnAutoStartMain) as TranslateTransform;
            var t2     = tpl?.FindName("transOn",          btnAutoStartMain) as TranslateTransform;
            var txtOn  = tpl?.FindName("txtOn",            btnAutoStartMain) as TextBlock;
            var txtAC  = tpl?.FindName("txtAutoConnect",   btnAutoStartMain) as TextBlock;
            if (t1 == null || t2 == null || txtOn == null || txtAC == null) return;

            var dur = new Duration(TimeSpan.FromMilliseconds(animate ? 300 : 0));
            t1.BeginAnimation(TranslateTransform.XProperty,    new DoubleAnimation(state ? -10 : 0,  dur));
            t2.BeginAnimation(TranslateTransform.XProperty,    new DoubleAnimation(state ? 44  : 0,  dur));
            txtOn.BeginAnimation(UIElement.OpacityProperty,    new DoubleAnimation(state ? 1   : 0,  dur));
            var targetColor = state
                ? System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0)
                : System.Windows.Media.Color.FromRgb(0xA0, 0xAE, 0xC0);
            var cloned = txtAC.Foreground is SolidColorBrush scb ? scb.Clone()
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0xAE, 0xC0));
            txtAC.Foreground = cloned;
            cloned.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(targetColor, dur));
        }

        private void SetAdvState(bool state)
        {
            var txt = btnAdvMain.Template?.FindName("txtAdv", btnAdvMain) as TextBlock;
            if (txt != null) txt.Foreground = state ? BrWhite : BrGray;
        }

        private void UpdateRoutingToggle()
        {
            btnProxyMode.Background  = System.Windows.Media.Brushes.Transparent;
            btnProxyMode.Foreground  = BrGray;
            btnClearProxy.Background = System.Windows.Media.Brushes.Transparent;
            btnClearProxy.Foreground = BrGray;

            var activeBridge = _state.IsEngineRunning ? _pollSelBridge : _cfg.LastBridge;
            bool isSnowflake = activeBridge == "snowflake";

            if (isSnowflake)
            {
                btnVpnMode.Background = System.Windows.Media.Brushes.Transparent;
                btnVpnMode.Foreground = BrDarkGray;
                btnVpnMode.Cursor     = System.Windows.Input.Cursors.Arrow;
                btnVpnMode.IsEnabled  = false;
                vpnToolTip.Content    = "VPN Mode is disabled when using the Snowflake bridge.";
                if (_cfg.LastXrayMode == "VPN Mode") _cfg.LastXrayMode = "Proxy Mode";
            }
            else
            {
                btnVpnMode.Background = System.Windows.Media.Brushes.Transparent;
                btnVpnMode.Foreground = BrGray;
                btnVpnMode.Cursor     = System.Windows.Input.Cursors.Hand;
                btnVpnMode.IsEnabled  = true;
                vpnToolTip.Content    = "Route your entire system's network globally through the secure tunnel.";
            }
            vpnToolTip.Visibility = Visibility.Visible;

            switch (_cfg.LastXrayMode)
            {
                case "Proxy Mode":
                    btnProxyMode.Background = BrActiveRoute;
                    btnProxyMode.Foreground = BrWhite;
                    break;
                case "VPN Mode" when !isSnowflake:
                    btnVpnMode.Background = BrActiveRoute;
                    btnVpnMode.Foreground = BrWhite;
                    break;
                case "Clear Proxy":
                    btnClearProxy.Background = BrActiveRoute;
                    btnClearProxy.Foreground = BrWhite;
                    break;
            }

            btnDirectLbl.IsEnabled = true; btnDirectLbl.Opacity = 1.0;
            btnDirectTog.IsEnabled = true; btnDirectTog.Opacity = 1.0;
        }

        private void EvaluateProxyExclusivity()
        {
            btnOutboundLbl.IsEnabled = true; btnOutboundLbl.Opacity = 1.0;
            btnOutboundTog.IsEnabled = true; btnOutboundTog.Opacity = 1.0;
        }

        // TOAST NOTIFICATION
        public void ShowToast(string message, bool success = false)
        {
            var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            delay.Tick += (s, e) =>
            {
                delay.Stop();
                var toast = new Window
                {
                    WindowStyle         = WindowStyle.None,
                    AllowsTransparency  = true,
                    Background          = System.Windows.Media.Brushes.Transparent,
                    ShowInTaskbar       = false,
                    Topmost             = true,
                    Owner               = this,
                    SizeToContent       = SizeToContent.WidthAndHeight,
                    IsHitTestVisible    = false,
                    ShowActivated       = false,
                    ResizeMode          = ResizeMode.NoResize
                };
                var border = new Border
                {
                    Background   = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xE6, 0x2D, 0x37, 0x48)),
                    CornerRadius = new CornerRadius(6),
                    Padding      = new Thickness(15, 5, 15, 5)
                };
                var txt = new TextBlock
                {
                    Text                = message,
                    Foreground          = success ? BrGreen : BrRed,
                    FontWeight          = FontWeights.Bold,
                    FontSize            = 13,
                    VerticalAlignment   = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                border.Child  = txt;
                toast.Content = border;
                toast.Opacity = 0;
                toast.WindowStartupLocation = WindowStartupLocation.Manual;
                toast.Show();
                toast.Left = Left + (ActualWidth  / 2) - (toast.ActualWidth  / 2);
                toast.Top  = Top  + ActualHeight       - toast.ActualHeight   - 20;

                var durIn  = new Duration(TimeSpan.FromMilliseconds(250));
                var durOut = new Duration(TimeSpan.FromMilliseconds(500));
                var sb = new Storyboard();
                
                var animIn = new DoubleAnimation(1, durIn);
                Storyboard.SetTarget(animIn, toast);
                Storyboard.SetTargetProperty(animIn, new PropertyPath(OpacityProperty));
                
                var animOut = new DoubleAnimation(0, durOut) { BeginTime = TimeSpan.FromMilliseconds(2750) };
                animOut.Completed += (s2, e2) => toast.Close();
                Storyboard.SetTarget(animOut, toast);
                Storyboard.SetTargetProperty(animOut, new PropertyPath(OpacityProperty));
                
                sb.Children.Add(animIn);
                sb.Children.Add(animOut);
                sb.Begin(toast);
            };
            delay.Start();
        }

        // RING ANIMATION
        private void UpdateRingAnimation(string state)
        {
            switch (state)
            {
                case "Idle":
                    ringCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, new Duration(TimeSpan.FromMilliseconds(400))));
                    AnimateGradientStop(scannerStop1, "#00C52034", 300);
                    AnimateGradientStop(scannerStop2, "#FFC52034", 300);
                    AnimateGradientStop(scannerStop3, "#00C52034", 300);
                    if (scannerTrans != null)
                    {
                        var toMiddleAnim = new DoubleAnimation(195, new Duration(TimeSpan.FromMilliseconds(500)))
                        {
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                        };
                        scannerTrans.BeginAnimation(TranslateTransform.XProperty, toMiddleAnim);
                    }
                    break;

                case "Connecting":
                    ringCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, new Duration(TimeSpan.FromMilliseconds(350))));
                    AnimateGradientStop(scannerStop1, "#00B78854", 300);
                    AnimateGradientStop(scannerStop2, "#FFB78854", 300);
                    AnimateGradientStop(scannerStop3, "#00B78854", 300);
                    if (scannerTrans != null)
                    {
                        var anim = new DoubleAnimationUsingKeyFrames
                        {
                            RepeatBehavior = RepeatBehavior.Forever
                        };
                        anim.KeyFrames.Add(new LinearDoubleKeyFrame(195, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                        anim.KeyFrames.Add(new EasingDoubleKeyFrame(290, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1250)), new SineEase { EasingMode = EasingMode.EaseOut }));
                        anim.KeyFrames.Add(new EasingDoubleKeyFrame(100, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3750)), new SineEase { EasingMode = EasingMode.EaseInOut }));
                        anim.KeyFrames.Add(new EasingDoubleKeyFrame(195, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(5000)), new SineEase { EasingMode = EasingMode.EaseIn }));
                        scannerTrans.BeginAnimation(TranslateTransform.XProperty, anim);
                    }
                    break;

                case "Connected":
                    ringCanvas.Opacity = 1.0;
                    AnimateGradientStop(scannerStop1, "#0068D391", 600);
                    AnimateGradientStop(scannerStop2, "#FF68D391", 600);
                    AnimateGradientStop(scannerStop3, "#0068D391", 600);
                    if (scannerTrans != null)
                    {
                        var toMiddleAnim = new DoubleAnimation(195, new Duration(TimeSpan.FromMilliseconds(500)))
                        {
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                        };
                        scannerTrans.BeginAnimation(TranslateTransform.XProperty, toMiddleAnim);
                    }
                    break;
            }
        }

        private static void AnimateGradientStop(GradientStop? stop, string hex, int ms)
        {
            if (stop == null) return;
            var col = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            stop.BeginAnimation(GradientStop.ColorProperty,
                new ColorAnimation(col, new Duration(TimeSpan.FromMilliseconds(ms))));
        }

        // WINDOW PANEL ANIMATION (Advanced + Logs expand/collapse)
        private void UpdateWindowSize()
        {
            _hideAdvTimer?.Stop();
            _hideLogTimer?.Stop();

            var ts      = new Duration(TimeSpan.FromMilliseconds(300));
            double targetW  = (_state.IsLogsOpen && _state.IsAdvancedOpen) ? 915 : 600;
            double targetH  = (_state.IsAdvancedOpen || _state.IsLogsOpen) ? 551 : 330;
            double panelTop = (_state.IsAdvancedOpen || _state.IsLogsOpen) ? 451 : 230;

            if (_state.IsAdvancedOpen || _state.IsLogsOpen)
            {
                UnifiedPanel.CornerRadius    = new CornerRadius(0, 0, 4, 4);
                UnifiedPanel.BorderThickness = new Thickness(1, 0, 1, 1);
            }
            else
            {
                UnifiedPanel.CornerRadius    = new CornerRadius(4);
                UnifiedPanel.BorderThickness = new Thickness(1);
            }

            double advOpac = 0;
            if (_state.IsAdvancedOpen)
            {
                AdvancedBorder.CornerRadius = new CornerRadius(4, 4, 0, 0);
                AdvancedCanvas.Visibility   = Visibility.Visible;
                advOpac = 1.0;
            }

            if (_state.IsLogsOpen)
            {
                LogsCanvas.Visibility = Visibility.Visible;
                logBorder.Height = targetH - 15 - 10;
                _logTimer?.Start();
                if (_state.IsAdvancedOpen) AnimateLogsSideMode(ts);
                else                       AnimateLogsBottomMode(ts);
                LogsCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(1, ts));
            }
            else
            {
                LogsCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(0, ts));
                _logTimer?.Stop();
            }

            this.BeginAnimation(WidthProperty,  new DoubleAnimation(targetW, ts));
            this.BeginAnimation(HeightProperty, new DoubleAnimation(targetH, ts));
            AdvancedCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(advOpac, ts));
            UnifiedPanel.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(panelTop, ts));

            if (!_state.IsAdvancedOpen)
            {
                _hideAdvTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _hideAdvTimer.Tick += (s, e) => { _hideAdvTimer.Stop(); if (!_state.IsAdvancedOpen) AdvancedCanvas.Visibility = Visibility.Hidden; };
                _hideAdvTimer.Start();
            }
            if (!_state.IsLogsOpen)
            {
                _hideLogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _hideLogTimer.Tick += (s, e) => { _hideLogTimer.Stop(); if (!_state.IsLogsOpen) LogsCanvas.Visibility = Visibility.Hidden; };
                _hideLogTimer.Start();
            }
        }

        private void AnimateLogsBottomMode(Duration ts)
        {
            LogsCanvas.BeginAnimation(Canvas.LeftProperty,   new DoubleAnimation(15,   ts));
            LogsCanvas.BeginAnimation(Canvas.TopProperty,    new DoubleAnimation(230,  ts));
            LogsCanvas.BeginAnimation(WidthProperty,         new DoubleAnimation(550,  ts));
            LogsCanvas.BeginAnimation(HeightProperty,        new DoubleAnimation(221,  ts));
            logBorder.CornerRadius = new CornerRadius(4, 4, 0, 0);
            logBorder.BeginAnimation(WidthProperty,          new DoubleAnimation(550,  ts));
            logBorder.BeginAnimation(HeightProperty,         new DoubleAnimation(221,  ts));
            btnCloseLogs.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(522,  ts));
            // Tor labels — bottom mode
            AnimateTorLabelsBottom(ts);
            logSeparator.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(152, ts));
            logSeparator.BeginAnimation(Canvas.TopProperty,  new DoubleAnimation(0,   ts));
            logSeparator.BeginAnimation(WidthProperty,       new DoubleAnimation(1,   ts));
            logSeparator.BeginAnimation(HeightProperty,      new DoubleAnimation(220, ts));
            lblConnTitle.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(169, ts));
            lblConnTitle.BeginAnimation(Canvas.TopProperty,  new DoubleAnimation(10,  ts));
            txtXrayLogs.BeginAnimation(Canvas.LeftProperty,  new DoubleAnimation(169, ts));
            txtXrayLogs.BeginAnimation(Canvas.TopProperty,   new DoubleAnimation(30,  ts));
            txtXrayLogs.BeginAnimation(WidthProperty,        new DoubleAnimation(364, ts));
            txtXrayLogs.BeginAnimation(HeightProperty,       new DoubleAnimation(177, ts));
        }

        private void AnimateLogsSideMode(Duration ts)
        {
            LogsCanvas.BeginAnimation(Canvas.LeftProperty,   new DoubleAnimation(580, ts));
            LogsCanvas.BeginAnimation(Canvas.TopProperty,    new DoubleAnimation(15,  ts));
            LogsCanvas.BeginAnimation(WidthProperty,         new DoubleAnimation(300, ts));
            LogsCanvas.BeginAnimation(HeightProperty,        new DoubleAnimation(501, ts));
            logBorder.CornerRadius = new CornerRadius(4);
            logBorder.BeginAnimation(WidthProperty,          new DoubleAnimation(300, ts));
            logBorder.BeginAnimation(HeightProperty,         new DoubleAnimation(501, ts));
            btnCloseLogs.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(272, ts));
            AnimateTorLabelsSide(ts);
            logSeparator.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(15,  ts));
            logSeparator.BeginAnimation(Canvas.TopProperty,  new DoubleAnimation(110, ts));
            logSeparator.BeginAnimation(WidthProperty,       new DoubleAnimation(270, ts));
            logSeparator.BeginAnimation(HeightProperty,      new DoubleAnimation(1,   ts));
            lblConnTitle.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(15,  ts));
            lblConnTitle.BeginAnimation(Canvas.TopProperty,  new DoubleAnimation(120, ts));
            txtXrayLogs.BeginAnimation(Canvas.LeftProperty,  new DoubleAnimation(15,  ts));
            txtXrayLogs.BeginAnimation(Canvas.TopProperty,   new DoubleAnimation(138, ts));
            txtXrayLogs.BeginAnimation(WidthProperty,        new DoubleAnimation(270, ts));
            txtXrayLogs.BeginAnimation(HeightProperty,       new DoubleAnimation(348, ts));
        }

        private void AnimateTorLabelsBottom(Duration ts)
        {
            double[] lefts = { 15, 15, 15, 15, 15, 15, 15, 15 };
            double[] tops  = { 33, 55, 77, 99, 121, 143, 165, 187 };
            AnimateTorLabels(lefts, tops, ts);
        }

        private void AnimateTorLabelsSide(Duration ts)
        {
            double[] lefts = { 15, 15, 15, 15, 155, 155, 155, 155 };
            double[] tops  = { 30, 48, 66, 84, 30, 48, 66, 84 };
            AnimateTorLabels(lefts, tops, ts);
        }

        private void AnimateTorLabels(double[] lefts, double[] tops, Duration ts)
        {
            var labels = new[] { lblTor1, lblTor2, lblTor3, lblTor4, lblTor5, lblTor6, lblTor7, lblTor8 };
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(lefts[i], ts));
                labels[i].BeginAnimation(Canvas.TopProperty,  new DoubleAnimation(tops[i],  ts));
            }
        }

        // GEO PING
        private void StartGeoPing()
        {
            if (_state.IsGeoTracing) return;
            _state.IsGeoTracing = true;
            lblGeoData.Text       = "Loc: TRACING...\nPing: --";
            lblGeoData.Foreground = BrGreen;

            if (_geoCts != null)
            {
                _geoCts.Cancel();
                _geoCts.Dispose();
            }
            _geoCts = new CancellationTokenSource();
            var token = _geoCts.Token;
            var sw = Stopwatch.StartNew();

            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _geoPingClient.GetStringAsync("https://get.geojs.io/v1/ip/geo.json", token);
                    sw.Stop();
                    var pingMs = sw.ElapsedMilliseconds;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _state.IsGeoTracing = false;
                        if (!_state.IsConnected) return;
                        var data = JObject.Parse(result);
                        var cMap = new Dictionary<string, string>
                        {
                            ["NA"] = "NORTH AMERICA", ["EU"] = "EUROPE", ["AS"] = "ASIA",
                            ["SA"] = "SOUTH AMERICA", ["AF"] = "AFRICA", ["OC"] = "OCEANIA", ["AN"] = "ANTARCTICA"
                        };
                        var continent = cMap.TryGetValue(data["continent_code"]?.ToString() ?? "", out var c) ? c : data["continent_code"]?.ToString() ?? "";
                        var config = GetComboTag(comboConfig) ?? "Optimized";
                        string geoStr;
                        if (_cfg.EnableV2rayChain || config == "Custom")
                            geoStr = data["country"]?.ToString() ?? "";
                        else if (config == "Expert" && !string.IsNullOrWhiteSpace(_cfg.ExpertExitNodes) && !_cfg.ExpertExitNodes.Contains(","))
                            geoStr = data["country"]?.ToString() ?? "";
                        else
                            geoStr = continent;

                        lblGeoData.Text       = $"Loc: {geoStr.ToUpper()}\nPing: {pingMs}ms";
                        lblGeoData.Foreground = BrGreen;
                    });
                }
                catch
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _state.IsGeoTracing = false;
                        if (_state.IsConnected)
                        {
                            lblGeoData.Text       = "Loc: TIMEOUT\nPing: --";
                            lblGeoData.Foreground = BrDarkRed;
                        }
                    });
                }
            }, token);
        }

        // HAPROXY CONFIG
        private void FormatHAProxyConfig(int activeCount)
        {
            var cfgPath = GetAppPath(@"Data\HAproxy\haproxy.cfg");
            if (!File.Exists(cfgPath)) return;
            var lines    = File.ReadAllLines(cfgPath).ToList();
            var newLines = new List<string>();

            foreach (var line in lines)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\s*(listen stats|bind 127\.0\.0\.1:10888|mode http|stats enable|stats uri /stats)"))
                {
                    continue;
                }

                var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*(?:#\s*)*server\s+tor(\d+)");
                if (m.Success)
                {
                    int idx = int.Parse(m.Groups[1].Value);
                    string cleanLine = System.Text.RegularExpressions.Regex.Replace(line, @"^\s*(?:#\s*)*", "");
                    
                    if (idx <= activeCount)
                        newLines.Add("    " + cleanLine);
                    else
                        newLines.Add("    # " + cleanLine);
                }
                else
                {
                    newLines.Add(line);
                }
            }

            while (newLines.Count > 0 && string.IsNullOrWhiteSpace(newLines.Last()))
                newLines.RemoveAt(newLines.Count - 1);

            if (!string.Join("\n", lines).Equals(string.Join("\n", newLines)))
                File.WriteAllLines(cfgPath, newLines);
        }

        // XRAY RESTART 
        private void RestartXray(string targetMode)
        {
            KillManagedProcess("xray");
            KillManagedProcess("sing-box");
            KillPidRef(ref _xrayDebugPid);
            KillPidRef(ref _sbDebugPid);
            KillPidRef(ref _xrayDohPid);

            _xrayRestartTimer?.Stop();
            _xrayRestartTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _xrayRestartTimer.Tick += (s, e) =>
            {
                _xrayRestartTimer.Stop();
                if (!XrayConfigWriter.Write(_cfg, _cfg.XrayDir)) return;

                if (_cfg.DebugMode)
                {
                    using (var p = Process.Start(new ProcessStartInfo("cmd.exe",
                               $"/c \"title XrayDebug & .\\xray.exe run -c config.json || pause\"")
                           { WorkingDirectory = _cfg.XrayDir, UseShellExecute = false }))
                    {
                        _xrayDebugPid = p?.Id;
                    }
                }
                else
                {
                    ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir);
                }

                if (targetMode == "VPN Mode")
                {
                    if (!SingboxConfigWriter.Write(_cfg, _cfg.SbDir)) return;
                    if (_cfg.DebugMode)
                    {
                        using (var p2 = Process.Start(new ProcessStartInfo("cmd.exe",
                                   $"/c \"title SingBoxDebug & .\\sing-box.exe run -c config.json || pause\"")
                               { WorkingDirectory = _cfg.SbDir, UseShellExecute = false }))
                        {
                            _sbDebugPid = p2?.Id;
                        }
                    }
                    else
                    {
                        ProcessService.StartProcessDirect(GetAppPath(@"Data\sing_box\sing-box.exe"), "run -c config.json", _cfg.SbDir);
                    }
                }

                ProxyService.SetSystemProxy(targetMode == "Proxy Mode");
                if (targetMode != "Proxy Mode") ProxyService.DisableSystemProxy();

                if (_state.IsConnected)
                {
                    if (_cfg.AllowLanConnections)
                    {
                        UpdateLanIp();
                        lblSocksDataIPs.Text  = $"127.0.0.1:10818\n{_state.LanIp}:10818";
                        lblSocksDataTags.Text = "(Local)\n(LAN)";
                    }
                    else
                    {
                        lblSocksDataIPs.Text  = "127.0.0.1:10818\n";
                        lblSocksDataTags.Text = "(Local)\n";
                    }

                    _pingTimer?.Stop();
                    var pt = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    pt.Tick += (s2, e2) => { pt.Stop(); StartGeoPing(); };
                    _pingTimer = pt;
                    pt.Start();
                }
            };
            _xrayRestartTimer.Start();
        }

        // STOP ALL ENGINES
        private void StopAllEngines(bool isClosing = false)
        {
            _state.AbortBoot       = true;
            _state.IsEngineRunning = false;
            _bootstrapTimer?.Stop();
            _staggerTimer?.Stop();
            ProxyService.DisableSystemProxy();

            // Kill all managed processes
            KillManagedProcess("tor");
            KillManagedProcess("haproxy");
            KillManagedProcess("xray");
            KillManagedProcess("sing-box");
            KillPidRef(ref _xrayDebugPid);
            KillPidRef(ref _sbDebugPid);
            KillPidRef(ref _xrayDohPid);

            _state.IsConnected      = false;
            _state.LastTotalBytes   = 0;
            _state.SessionDataBytes = 0;
            _state.SessionStartTime = null;
            _state.SpeedSamples     = new double[5];

            for (int i = 1; i <= 8; i++)
                TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));
            TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
            TryDeleteFile(GetAppPath(@"Data\Xray\error.log"));
            TryDeleteFile(GetAppPath(@"Data\Xray\access.log.tmp"));

            if (!isClosing)
            {
                _lastXrayLogPos = 0;
                _xrayLogLines.Clear();
                if (txtXrayLogs != null) txtXrayLogs.Text = "";

                lblSessionTime.Text       = "SESSION: OFFLINE";
                lblSessionTime.Foreground = BrGray;
                ResetButtonText();
                btnAction.IsEnabled       = true;
                lblSocksTitle.Text        = "MIXED PORT";
                lblSocksDataIPs.Text      = "Waiting for connection...";
                lblSocksDataTags.Text     = "";
                lblStatsData.Text         = "Speed: 0 KB/s\nTotal: 0 MB";
                lblGeoData.Text           = "Loc: --\nPing: --";
                lblGeoData.Foreground     = BrGreen;
                UpdateRoutingToggle();
            }
        }

        private void ResetButtonText()
        {
            ChangeTextWithFade(btnActionMainText, "CONNECT", BrWhite);
            SetSubText("");
            UpdateRingAnimation("Idle");
        }

        // START ENGINES
        private async void StartEnginesAsync()
        {
            _state.IsEngineRunning = true;
            UpdateLanIp();

            ChangeTextWithFade(btnActionMainText, "CONNECTING", BrOrange);
            SetSubText("Clearing old engines...", new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB7, 0x88, 0x54)));

            // Kill lingering processes
            for (int i = 0; i < 8; i++) KillPidRef(ref _torPids[i]);
            KillManagedProcess("haproxy");
            KillManagedProcess("xray");
            KillManagedProcess("sing-box");
            KillPidRef(ref _xrayDebugPid);
            KillPidRef(ref _sbDebugPid);
            KillPidRef(ref _xrayDohPid);
            _bootstrapTimer?.Stop();
            _staggerTimer?.Stop();
            ProxyService.DisableSystemProxy();

            // Reset state
            _state.IsConnected      = false;
            _state.LastTotalBytes   = 0;
            _state.SessionDataBytes = 0;
            _state.SessionStartTime = null;
            _state.SpeedSamples     = new double[5];
            _torPids                = new int?[8];
            _state.AbortBoot        = false;

            await Task.Delay(800);
            if (_state.AbortBoot) return;

            var selBridge = GetComboTag(comboBridge) ?? "meek_lite";
            var selConfig = GetComboTag(comboConfig) ?? "Optimized";
            var selCount  = int.Parse(GetComboTag(comboCount) ?? "1");

            _pollSelCount  = selCount;
            _pollMode      = _cfg.LastXrayMode;
            _pollSelBridge = selBridge;

            for (int i = 1; i <= 8; i++)
                TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));

            // Init tor labels
            for (int i = 1; i <= 8; i++)
            {
                var lbl = FindName($"lblTor{i}") as TextBlock;
                if (lbl == null) continue;
                var padded = i.ToString().PadLeft(2, '0');
                if (i <= _pollSelCount) { lbl.Text = $"Tor {padded}: Waiting..."; lbl.Foreground = BrGray; }
                else               { lbl.Text = $"Tor {padded}: Disabled";   lbl.Foreground = BrDarkGray; }
            }
            // Clear xray logs
            TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
            TryDeleteFile(GetAppPath(@"Data\Xray\access.log.tmp"));
            _lastXrayLogPos = 0;
            _xrayLogLines.Clear();
            if (txtXrayLogs != null) txtXrayLogs.Text = "";

            SaveConfig();

            UpdateRingAnimation("Connecting");
            FormatHAProxyConfig(_pollSelCount);

            // Launch Tor instances staggered
            for (int i = 1; i <= _pollSelCount; i++)
            {
                if (_state.AbortBoot) break;
                var padded = i.ToString().PadLeft(2, '0');
                SetSubText($"Launching Tor {i} of {_pollSelCount}");

                var torPath    = GetAppPath($@"Data\Tors\Tor{i}");
                var torrcFile  = "torrc";

                if (!File.Exists(Path.Combine(torPath, torrcFile))) continue;

                var lines = TorrcBuilder.BuildTorrcConfig(torrcFile, _pollSelBridge, selConfig, torPath, _cfg);
                File.WriteAllLines(Path.Combine(torPath, torrcFile), lines);

                var idx = i; 
                using (var proc = ProcessService.StartProcessDirect(
                           GetAppPath(@"Data\TorBin\tor.exe"),
                           $"-f {torrcFile}",
                           torPath,
                           hidden: !_cfg.DebugMode))
                {
                    if (proc != null) _torPids[idx - 1] = proc.Id;
                }
                
                await Task.Delay(1500);
            }

            if (_state.AbortBoot) return;

            FormatHAProxyConfig(_pollSelCount);

            // Bootstrap polling
            bool isBridged     = selBridge != "Direct (None)";
            int hardTimeout    = isBridged ? 300 : 180;
            var deadline       = DateTime.Now.AddSeconds(hardTimeout);
            SetSubText("Waiting for Tor bootstrap...");

            _bootstrapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _bootstrapTimer.Tick += (s, e) => BootstrapTick(selConfig, deadline);
            _bootstrapTimer.Start();
        }

        private void BootstrapTick(string selConfig, DateTime deadline)
        {
            if (_state.AbortBoot) { _bootstrapTimer?.Stop(); ResetButtonText(); return; }

            bool oneReady  = false;
            int  bestPct   = -1;
            int  bestTorIdx = 1;

            for (int i = 1; i <= _pollSelCount; i++)
            {
                var pct = ProcessService.ReadBootstrapPct(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));
                if (pct > bestPct) { bestPct = pct; bestTorIdx = i; }
                if (pct == 100) { oneReady = true; }
            }

            if (!oneReady)
            {
                if (DateTime.Now >= deadline)
                    SetSubText("Still Bootstrapping, Consider different bridges");
                else if (bestPct >= 0)
                    SetSubText($"Bootstrapping... {bestPct}% (Tor {bestTorIdx})");
                return;
            }

            _bootstrapTimer?.Stop();
            SetSubText("Booting Core Engines");

            TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
            TryDeleteFile(GetAppPath(@"Data\Xray\error.log"));

            var haExe = GetAppPath(@"Data\HAproxy\haproxy.exe");
            if (File.Exists(haExe))
                ProcessService.StartProcessDirect(haExe, "-f haproxy.cfg", _cfg.HaPath, hidden: !_cfg.DebugMode);

            KillManagedProcess("xray");
            KillManagedProcess("sing-box");
            KillPidRef(ref _xrayDebugPid);
            KillPidRef(ref _sbDebugPid);
            KillPidRef(ref _xrayDohPid);

            var xrayBootTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            xrayBootTimer.Tick += (s2, e2) =>
            {
                xrayBootTimer.Stop();
                if (_state.AbortBoot) return;

                if (!XrayConfigWriter.Write(_cfg, _cfg.XrayDir)) return;

                if (_cfg.DebugMode)
                {
                    using (var p = Process.Start(new ProcessStartInfo("cmd.exe",
                               $"/c \"title XrayDebug & .\\xray.exe run -c config.json || pause\"")
                           { WorkingDirectory = _cfg.XrayDir, UseShellExecute = false }))
                    {
                        _xrayDebugPid = p?.Id;
                    }
                }
                else
                    ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir);

                if (_pollMode == "VPN Mode")
                {
                    if (!SingboxConfigWriter.Write(_cfg, _cfg.SbDir)) return;
                    if (_cfg.DebugMode)
                    {
                        using (var p2 = Process.Start(new ProcessStartInfo("cmd.exe",
                                   $"/c \"title SingBoxDebug & .\\sing-box.exe run -c config.json || pause\"")
                               { WorkingDirectory = _cfg.SbDir, UseShellExecute = false }))
                        {
                            _sbDebugPid = p2?.Id;
                        }
                    }
                    else
                        ProcessService.StartProcessDirect(GetAppPath(@"Data\sing_box\sing-box.exe"), "run -c config.json", _cfg.SbDir);
                }

                ProxyService.SetSystemProxy(_pollMode == "Proxy Mode");
                if (_pollMode != "Proxy Mode") ProxyService.DisableSystemProxy();

                lblSocksTitle.Text    = "MIXED PORT";
                if (_cfg.AllowLanConnections)
                {
                    lblSocksDataIPs.Text  = $"127.0.0.1:10818\n{_state.LanIp}:10818";
                    lblSocksDataTags.Text = "(Local)\n(LAN)";
                }
                else
                {
                    lblSocksDataIPs.Text  = "127.0.0.1:10818\n";
                    lblSocksDataTags.Text = "(Local)\n";
                }
                _state.IsConnected    = true;
                _state.SessionStartTime = DateTime.Now;
                ChangeTextWithFade(btnActionMainText, "CONNECTED", BrGreen);
                SetSubText("");
                StartGeoPing();
                UpdateRingAnimation("Connected");
            };
            xrayBootTimer.Start();
        }

        // TIMERS SETUP
        private void InitTimers()
        {
            // Session clock
            _sessionClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sessionClockTimer.Tick += (s, e) =>
            {
                if (_state.IsConnected && _state.SessionStartTime.HasValue)
                {
                    var elapsed = DateTime.Now - _state.SessionStartTime.Value;
                    lblSessionTime.Text       = "SESSION: " + elapsed.ToString(@"hh\:mm\:ss");
                    lblSessionTime.Foreground = BrGreen;
                }
            };
            _sessionClockTimer.Start();

            // Stats timer (queries Xray gRPC stats)
            bool isFetchingStats = false;
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statsTimer.Tick += async (s, e) =>
            {
                if (!_state.IsConnected || isFetchingStats) return;
                isFetchingStats = true;

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:10899/xray.app.stats.command.StatsService/QueryStats")
                    {
                        Version = new Version(2, 0),
                        VersionPolicy = HttpVersionPolicy.RequestVersionExact
                    };
                    request.Content = new ByteArrayContent(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x02, 0x0A, 0x00 });
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
                    request.Headers.Add("TE", "trailers");
                    
                    using var response = await _xrayGrpcClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        System.IO.File.AppendAllText("stats_error.txt", $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n");
                    }
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    
                    long upVal = 0, dnVal = 0;
                    int pos = 5;
                    while (pos < bytes.Length)
                    {
                        if (bytes[pos] == 0x0A)
                        {
                            pos++;
                            int statLen = ReadVarint(bytes, ref pos);
                            int statEnd = pos + statLen;
                            string name = "";
                            long value = 0;
                            while (pos < statEnd)
                            {
                                int tag = ReadVarint(bytes, ref pos);
                                if (tag == 0x0A)
                                {
                                    int nameLen = ReadVarint(bytes, ref pos);
                                    name = System.Text.Encoding.UTF8.GetString(bytes, pos, nameLen);
                                    pos += nameLen;
                                }
                                else if (tag == 0x10)
                                {
                                    value = ReadVarint64(bytes, ref pos);
                                }
                                else
                                {
                                    int wireType = tag & 7;
                                    if (wireType == 0) ReadVarint64(bytes, ref pos);
                                    else if (wireType == 1) pos += 8;
                                    else if (wireType == 2) pos += ReadVarint(bytes, ref pos);
                                    else if (wireType == 5) pos += 4;
                                }
                            }
                            if (name.Contains("uplink")) upVal += value;
                            if (name.Contains("downlink")) dnVal += value;
                        }
                        else break;
                    }

                    long curBytes = upVal + dnVal;
                    if (curBytes > 0 && _state.LastTotalBytes > 0)
                    {
                        var diff = Math.Max(0, curBytes - _state.LastTotalBytes);
                        _state.SessionDataBytes += diff;
                        _state.SpeedSamples = new double[] { diff }.Concat(_state.SpeedSamples.Take(4)).ToArray();
                        var avg = _state.SpeedSamples.Average() / 2;
                        
                        string spd = avg >= 1048576 ? $"{Math.Round(avg / 1048576, 2)} MB/s"
                                   : avg >= 1024    ? $"{Math.Round(avg / 1024, 1)} KB/s"
                                   :                  $"{(int)avg} B/s";
                        string tot = _state.SessionDataBytes >= 1073741824 ? $"{Math.Round(_state.SessionDataBytes / 1073741824.0, 2)} GB"
                                   : _state.SessionDataBytes >= 1048576    ? $"{Math.Round(_state.SessionDataBytes / 1048576.0, 1)} MB"
                                   :                                       $"{Math.Round(_state.SessionDataBytes / 1024.0, 1)} KB";
                        
                        lblStatsData.Text = $"Speed: {spd}\nTotal: {tot}";
                    }
                    if (curBytes > 0) _state.LastTotalBytes = curBytes;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Stats error: {ex.Message}"); }
                isFetchingStats = false;
            };
            _statsTimer.Start();

            // Live log timer
            _logTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _logTimer.Tick += (s, e) => UpdateLiveLogs();

            // Log auto-cleaner (every 2h)
            _logClearTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(2) };
            _logClearTimer.Tick += (s, e) =>
            {
                foreach (var lf in new[] { @"Data\Xray\access.log", @"Data\Xray\error.log" })
                {
                    var fp = GetAppPath(lf);
                    if (File.Exists(fp))
                        try { using var fs = new FileStream(fp, FileMode.Truncate); } catch { }
                }
            };
            _logClearTimer.Start();
        }

        private void UpdateLiveLogs()
        {
            if (!_state.IsLogsOpen) return;
            var uiSelCount = int.Parse(GetComboTag(comboCount) ?? "1");
            var selCount = _state.IsEngineRunning ? _pollSelCount : uiSelCount;
            var labels   = new[] { lblTor1, lblTor2, lblTor3, lblTor4, lblTor5, lblTor6, lblTor7, lblTor8 };

            if (!_state.IsEngineRunning)
            {
                for (int i = 0; i < 8; i++)
                {
                    var padded = (i + 1).ToString().PadLeft(2, '0');
                    labels[i].Text       = i < selCount ? $"Tor {padded}: Offline" : $"Tor {padded}: Disabled";
                    labels[i].Foreground = BrDarkGray;
                }
                if (txtXrayLogs != null) txtXrayLogs.Text = "";
                return;
            }

            for (int i = 1; i <= 8; i++)
            {
                var lbl    = labels[i - 1];
                var padded = i.ToString().PadLeft(2, '0');
                if (i > selCount) { lbl.Text = $"Tor {padded}: Disabled"; lbl.Foreground = BrDarkGray; continue; }
                if (lbl.Text.Contains("100%")) continue;

                var pct = ProcessService.ReadBootstrapPct(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));
                if (pct >= 0)
                {
                    lbl.Text       = $"Tor {padded}: {pct}%";
                    lbl.Foreground = pct == 100 ? BrGreen : BrOrange;
                }
                else
                {
                    lbl.Text       = $"Tor {padded}: Booting...";
                    lbl.Foreground = BrGray;
                }
            }

            // Xray connection log
            var xrayLog = GetAppPath(@"Data\Xray\access.log");
            if (File.Exists(xrayLog) && txtXrayLogs != null)
            {
                try
                {
                    using var fs = new FileStream(xrayLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (fs.Length < _lastXrayLogPos) _lastXrayLogPos = 0; 
                    if (fs.Length > _lastXrayLogPos)
                    {
                        fs.Seek(_lastXrayLogPos, SeekOrigin.Begin);
                        using var sr = new StreamReader(fs);
                        var newContent = sr.ReadToEnd();
                        _lastXrayLogPos = fs.Length;

                        var newLines = newContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(l => (l.Contains("accepted") || l.Contains("proxy")) && !l.Contains(":10899"))
                            .Select(l => 
                            {
                                int firstSpace = l.IndexOf(' ');
                                if (firstSpace > 0)
                                {
                                    int secondSpace = l.IndexOf(' ', firstSpace + 1);
                                    if (secondSpace > 0)
                                    {
                                        string rest = l.Substring(secondSpace + 1);
                                        if (rest.StartsWith("127.0.0.1:"))
                                        {
                                            int thirdSpace = rest.IndexOf(' ');
                                            if (thirdSpace > 0) return rest.Substring(thirdSpace + 1);
                                        }
                                        return rest;
                                    }
                                }
                                return l;
                            });
                            
                        if (newLines.Any())
                        {
                            _xrayLogLines.AddRange(newLines);
                            if (_xrayLogLines.Count > 15) _xrayLogLines = _xrayLogLines.TakeLast(15).ToList();
                            txtXrayLogs.Text = string.Join("\n", _xrayLogLines);
                            txtXrayLogs.ScrollToEnd();
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error reading xray log: {ex.Message}"); }
            }
        }

        // TRAY ICON
        private void InitTrayIcon()
        {
            var iconPath = GetAppPath("icon.ico");
            if (!File.Exists(iconPath)) return;

            _trayIcon = new NotifyIcon
            {
                Icon    = new System.Drawing.Icon(iconPath),
                Text    = "CrimsonOnion",
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => RestoreWindow();
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show Window").Click      += (s, e) => RestoreWindow();
            menu.Items.Add("Exit Application").Click += (s, e) => Dispatcher.Invoke(Close);
            _trayIcon.ContextMenuStrip = menu;

            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    if (_cfg.MinimizeToTray)
                        Hide();
                }
            };
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        // BUTTON EVENTS
        private void WireEvents()
        {
            // Combos
            comboBridge.SelectionChanged += OnBridgeChanged;
            comboConfig.SelectionChanged += OnConfigChanged;
            comboCount.SelectionChanged  += OnCountChanged;

            // Main actions
            btnAction.Click += (s, e) =>
            {
                if (_state.IsConnected || btnActionMainText.Text == "CONNECTING")
                    StopAllEngines();
                else
                    StartEnginesAsync();
            };
            btnAutoStartMain.Click += (s, e) =>
            {
                _cfg.AutoStart = !_cfg.AutoStart;
                SetAutoConnectState(_cfg.AutoStart, true);
                RequestConfigSave();
            };
            btnAdvMain.Click += (s, e) =>
            {
                _state.IsAdvancedOpen = !_state.IsAdvancedOpen;
                SetAdvState(_state.IsAdvancedOpen);
                UpdateWindowSize();
            };
            btnDesktop.Click += (s, e) =>
            {
                try
                {
                    var ws = (dynamic)Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                    dynamic sc = ws.CreateShortcut(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CrimsonOnion.lnk"));
                    sc.TargetPath       = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    sc.WorkingDirectory = _cfg.BaseDir;
                    sc.Save();
                    ShowToast("Desktop shortcut created successfully!", success: true);
                }
                catch { ShowToast("Failed to create shortcut."); }
            };
            btnStatsPanel.Click += (s, e) => { if (_state.IsConnected) StartGeoPing(); };
            btnCloseLogs.Click  += (s, e) =>
            {
                _state.IsLogsOpen = false;
                SetLogsToggle(false);
                UpdateWindowSize();
                RequestConfigSave();
            };

            // Routing toggles
            btnProxyMode.Click  += (s, e) => ToggleMode("Proxy Mode");
            btnVpnMode.Click    += (s, e) => { if (GetComboTag(comboBridge) != "snowflake") ToggleMode("VPN Mode"); };
            btnClearProxy.Click += (s, e) => ToggleMode("Clear Proxy");

            // Advanced toggles
            btnV2rayTog.Click += (s, e) =>
            {
                if (!_cfg.EnableV2rayChain && string.IsNullOrWhiteSpace(_cfg.V2rayChainJson))
                    if (!ShowV2rayDialog()) return;
                _cfg.EnableV2rayChain = !_cfg.EnableV2rayChain;
                SetToggle(btnV2rayTog, _cfg.EnableV2rayChain);
                RequestConfigSave();
                SmartRestartXray();
            };
            btnV2rayLbl.Click += (s, e) => { ShowV2rayDialog(); SetToggle(btnV2rayTog, _cfg.EnableV2rayChain); };

            btnDirectTog.Click += (s, e) =>
            {
                bool newState = !_cfg.EnableDirect;
                if (newState && string.IsNullOrWhiteSpace(_cfg.LastManualSplit) && string.IsNullOrWhiteSpace(_cfg.LastAppSplit))
                    if (!ShowDirectRulesDialog()) return;
                _cfg.EnableDirect = newState;
                SetToggle(btnDirectTog, _cfg.EnableDirect);
                RequestConfigSave();
                SmartRestartXray();
            };
            btnDirectLbl.Click += (s, e) => ShowDirectRulesDialog();

            btnOutboundTog.Click += (s, e) =>
            {
                bool newState = !_cfg.EnableOutboundProxy;
                if (newState && string.IsNullOrWhiteSpace(_cfg.OutboundProxyAddress))
                    if (!ShowOutboundProxyDialog()) return;
                _cfg.EnableOutboundProxy = newState;
                SetToggle(btnOutboundTog, _cfg.EnableOutboundProxy);
                RequestConfigSave();
                if (_state.IsEngineRunning) ShowToast("Please reconnect to apply the changes.");
            };
            btnOutboundLbl.Click += (s, e) =>
            {
                if (ShowOutboundProxyDialog())
                {
                    SetToggle(btnOutboundTog, _cfg.EnableOutboundProxy);
                    if (_state.IsEngineRunning) ShowToast("Please reconnect to apply the changes.");
                }
            };

            btnDohTog.Click += (s, e) =>
            {
                if (_cfg.EnableUpstreamDoh)
                {
                    _cfg.EnableUpstreamDoh = false;
                    SetToggle(btnDohTog, false);
                    RequestConfigSave();
                    EvaluateProxyExclusivity();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(_cfg.UpstreamDohUrl))
                    {
                        if (!ShowDohDialog()) { SetToggle(btnDohTog, false); return; }
                    }
                    _cfg.EnableUpstreamDoh = true;
                    SetToggle(btnDohTog, true);
                    RequestConfigSave();
                    EvaluateProxyExclusivity();
                }
                SmartRestartXray();
            };
            btnDohLbl.Click += (s, e) =>
            {
                if (ShowDohDialog())
                {
                    SetToggle(btnDohTog, _cfg.EnableUpstreamDoh);
                    SmartRestartXray();
                }
            };

            btnBootTog.Click += (s, e) =>
            {
                _cfg.LaunchOnBoot = !_cfg.LaunchOnBoot;
                SetToggle(btnBootTog, _cfg.LaunchOnBoot);
                try { ProcessService.UpdateBootScheduledTask(_cfg.LaunchOnBoot, Process.GetCurrentProcess().MainModule?.FileName ?? "", _cfg.BaseDir); }
                catch { ShowToast("Failed to create Auto-Start task (Check Permissions)."); _cfg.LaunchOnBoot = false; SetToggle(btnBootTog, false); }
                RequestConfigSave();
            };
            btnStartMinTog.Click += (s, e) =>
            {
                _cfg.StartMinimized = !_cfg.StartMinimized;
                SetToggle(btnStartMinTog, _cfg.StartMinimized);
                RequestConfigSave();
            };
            btnDebugTog.Click += (s, e) =>
            {
                _cfg.DebugMode = !_cfg.DebugMode;
                SetToggle(btnDebugTog, _cfg.DebugMode);
                RequestConfigSave();
            };
            btnLanTog.Click += (s, e) =>
            {
                _cfg.AllowLanConnections = !_cfg.AllowLanConnections;
                SetToggle(btnLanTog, _cfg.AllowLanConnections);
                RequestConfigSave();
                SmartRestartXray();
            };
            btnTrayTog.Click += (s, e) =>
            {
                _cfg.MinimizeToTray = !_cfg.MinimizeToTray;
                SetToggle(btnTrayTog, _cfg.MinimizeToTray);
                RequestConfigSave();
            };
            btnLogsTog.Click += (s, e) =>
            {
                _state.IsLogsOpen = !_state.IsLogsOpen;
                SetLogsToggle(_state.IsLogsOpen);
                UpdateWindowSize();
                RequestConfigSave();
            };
            btnAdBlockTog.Click += (s, e) =>
            {
                _cfg.EnableAdBlock = !_cfg.EnableAdBlock;
                SetToggle(btnAdBlockTog, _cfg.EnableAdBlock);
                RequestConfigSave();
                SmartRestartXray();
            };

            // Label → toggle relay
            btnBootLbl.Click     += (s, e) => btnBootTog.RaiseEvent(    new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            btnStartMinLbl.Click += (s, e) => btnStartMinTog.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            btnTrayLbl.Click     += (s, e) => btnTrayTog.RaiseEvent(    new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            btnDebugLbl.Click    += (s, e) => btnDebugTog.RaiseEvent(   new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            btnLogsLbl.Click     += (s, e) => btnLogsTog.RaiseEvent(    new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            btnAdBlockLbl.Click  += (s, e) => btnAdBlockTog.RaiseEvent( new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            btnLanLbl.Click      += (s, e) => btnLanTog.RaiseEvent(     new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            // Update button
            btnTitleUpdate.Click += (s, e) => UpdateApplication();
        }

        private void ToggleMode(string mode)
        {
            if (_cfg.LastXrayMode == mode) return;
            _cfg.LastXrayMode = mode;
            UpdateRoutingToggle();
            RequestConfigSave();
            if (_state.IsConnected) RestartXray(mode);
        }

        private void SmartRestartXray()
        {
            if (_state.IsConnected)
            {
                if (_cfg.LastXrayMode == "VPN Mode") ShowToast("Please reconnect to apply the changes safely.");
                else RestartXray(_cfg.LastXrayMode);
            }
            else if (_state.IsEngineRunning)
                ShowToast("Please reconnect to apply the changes.");
        }

        // COMBO EVENTS
        private void OnBridgeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_state.IgnoreComboChange) return;
            var tag = GetComboTag(comboBridge) ?? "";
            if (tag == "Custom")
            {
                _state.IgnoreComboChange = true;
                if (!ShowCustomBridgeDialog()) SetComboTag(comboBridge, _state.PreviousBridge);
                else _state.PreviousBridge = "Custom";
                _state.IgnoreComboChange = false;
            }
            else { _state.PreviousBridge = tag; }

            if (_state.PreviousBridge == "snowflake" && _cfg.LastXrayMode == "VPN Mode")
            {
                _cfg.LastXrayMode = "Proxy Mode";
                if (_state.IsConnected) RestartXray("Proxy Mode");
            }
            _cfg.LastBridge = _state.PreviousBridge;
            UpdateRoutingToggle();
            SaveConfig();
            if (_state.IsEngineRunning) ShowToast("Please reconnect to apply the changes.");
        }

        private void OnConfigChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_state.IgnoreComboChange) return;
            var tag = GetComboTag(comboConfig) ?? "";
            if (tag == "Custom")
            {
                _state.IgnoreComboChange = true;
                if (!ShowExitNodeDialog()) SetComboTag(comboConfig, _state.PreviousConfig);
                else _state.PreviousConfig = "Custom";
                _state.IgnoreComboChange = false;
            }
            else if (tag == "Expert")
            {
                _state.IgnoreComboChange = true;
                if (!ShowExpertConfigDialog()) SetComboTag(comboConfig, _state.PreviousConfig);
                else _state.PreviousConfig = "Expert";
                _state.IgnoreComboChange = false;
            }
            else { _state.PreviousConfig = tag; }
            RequestConfigSave();
            if (_state.IsEngineRunning) ShowToast("Please reconnect to apply the changes.");
        }

        private void OnCountChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_state.IgnoreComboChange) return;

            RequestConfigSave();

            if (_state.IsConnected)
            {
                ShowToast("Please reconnect to apply the changes.");
                return;
            }

            if (!_state.IsEngineRunning) return;

            var newCount = int.Parse(GetComboTag(comboCount) ?? "1");
            var curCount = _pollSelCount;

            if (newCount < curCount)
            {
                for (int i = newCount + 1; i <= 8; i++)
                {
                    KillPidRef(ref _torPids[i - 1]);
                    var lbl    = FindName($"lblTor{i}") as TextBlock;
                    var padded = i.ToString().PadLeft(2, '0');
                    if (lbl != null) { lbl.Text = $"Tor {padded}: Disabled"; lbl.Foreground = BrDarkGray; }
                    _staggerQueue.Remove(i);
                }
                _pollSelCount = newCount;
                FormatHAProxyConfig(newCount);
            }
            else if (newCount > curCount)
            {
                _pollSelCount = newCount;
                FormatHAProxyConfig(newCount);
                bool isBootstrapping = _bootstrapTimer?.IsEnabled == true;
                for (int i = curCount + 1; i <= newCount; i++)
                {
                    var lbl    = FindName($"lblTor{i}") as TextBlock;
                    var padded = i.ToString().PadLeft(2, '0');
                    if (lbl != null) { lbl.Text = $"Tor {padded}: Waiting..."; lbl.Foreground = BrGray; }
                    TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));
                    if (isBootstrapping) _staggerQueue.Add(i);
                }
                if (isBootstrapping && _staggerQueue.Count > 0)
                {
                    if (_staggerTimer?.IsEnabled != true)
                    {
                        _staggerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                        _staggerTimer.Tick += (s, e) =>
                        {
                            if (_state.AbortBoot || !_state.IsEngineRunning || _staggerQueue.Count == 0)
                            { _staggerTimer?.Stop(); return; }
                            int idx = _staggerQueue[0]; _staggerQueue.RemoveAt(0);
                            LaunchSingleTor(idx);
                            if (_staggerQueue.Count == 0) _staggerTimer?.Stop();
                        };
                        _staggerTimer.Start();
                    }
                }
            }
        }

        private void LaunchSingleTor(int i)
        {
            var torPath   = GetAppPath($@"Data\Tors\Tor{i}");
            var torrcFile = "torrc";
            var selConfig = GetComboTag(comboConfig) ?? "Optimized";
            TryDeleteFile(Path.Combine(torPath, "tor.log"));
            if (!File.Exists(Path.Combine(torPath, torrcFile))) return;
            var lines = TorrcBuilder.BuildTorrcConfig(torrcFile, _pollSelBridge, selConfig, torPath, _cfg);
            File.WriteAllLines(Path.Combine(torPath, torrcFile), lines);
            var proc = ProcessService.StartProcessDirect(
                GetAppPath(@"Data\TorBin\tor.exe"), $"-f {torrcFile}", torPath, hidden: true);
            if (proc != null) _torPids[i - 1] = proc.Id;
        }

        // DIALOG LAUNCHERS
        private bool ShowCustomBridgeDialog()
        {
            var dlg = new CustomBridgeDialog(_cfg, s => ShowToast(s)) { Owner = this };
            return dlg.ShowDialog() == true;
        }

        private bool ShowDirectRulesDialog()
        {
            bool isVpn = _cfg.LastXrayMode == "VPN Mode";
            var dlg    = new DirectRulesDialog(_cfg, isVpn) { Owner = this };
            bool ok    = dlg.ShowDialog() == true;
            if (ok)
            {
                bool hasRules = !string.IsNullOrWhiteSpace(_cfg.LastManualSplit) ||
                                !string.IsNullOrWhiteSpace(_cfg.LastAppSplit);
                if (!_cfg.EnableDirect && hasRules)
                {
                    _cfg.EnableDirect = true;
                    SetToggle(btnDirectTog, true);
                }
                SaveConfig();
                SmartRestartXray();
            }
            return ok;
        }

        private bool ShowOutboundProxyDialog()
        {
            var dlg = new OutboundProxyDialog(_cfg) { Owner = this };
            return dlg.ShowDialog() == true;
        }

        private bool ShowDohDialog()
        {
            var dlg = new DohDialog(_cfg) { Owner = this };
            return dlg.ShowDialog() == true;
        }

        private bool ShowExitNodeDialog()
        {
            var dlg = new ExitNodeDialog(_cfg) { Owner = this };
            bool ok = dlg.ShowDialog() == true;
            if (ok && _state.IsEngineRunning) ShowToast("Please reconnect to apply the changes.");
            return ok;
        }

        private bool ShowExpertConfigDialog()
        {
            var dlg = new ExpertConfigDialog(_cfg) { Owner = this };
            bool ok = dlg.ShowDialog() == true;
            if (ok && _state.IsEngineRunning) ShowToast("Please reconnect to apply the changes.");
            return ok;
        }

        private bool ShowV2rayDialog()
        {
            var dlg = new V2rayDialog(_cfg, s => ShowToast(s)) { Owner = this };
            return dlg.ShowDialog() == true;
        }

        // CONFIG SAVE (debounced)
        private void RequestConfigSave()
        {
            _saveDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Tick += (s, e) => { _saveDebounceTimer.Stop(); SaveConfig(); };
            _saveDebounceTimer.Start();
        }

        private void SaveConfig()
        {
            _cfg.LastBridge = GetComboTag(comboBridge) ?? _cfg.LastBridge;
            _cfg.LastConfig = GetComboTag(comboConfig) ?? _cfg.LastConfig;
            _cfg.LastCount  = GetComboTag(comboCount)  ?? _cfg.LastCount;
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
        }

        // UPDATE CHECK
        private void CheckUpdateSilent()
        {
            Task.Run(async () =>
            {
                try
                {
                    using var wc = new HttpClient();
                    wc.Timeout = TimeSpan.FromSeconds(10);
                    var raw = await wc.GetStringAsync("https://raw.githubusercontent.com/RichTiTAN/CrimsonOnion/main/version.json");
                    var json = JObject.Parse(raw);
                    var remoteVer = json["version"]?.ToString() ?? "0.0.0";
                    
                    if (Version.Parse(remoteVer) > Version.Parse(AppVersion))
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_updateCts == null)
                                lblTitleText.Text = $"CrimsonOnion v{AppVersion}  -  UPDATE AVAILABLE";
                        });
                    }
                }
                catch { }
            });
        }

        private void UpdateApplication()
        {
            if (_updateCts != null)
            {
                _updateCts.Cancel();
                return;
            }

            lblTitleText.Text = "CHECKING FOR UPDATES...";
            _updateCts = new CancellationTokenSource();
            var token = _updateCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    using var wc = new HttpClient();
                    wc.Timeout   = TimeSpan.FromSeconds(15);
                    var response = await wc.GetAsync("https://raw.githubusercontent.com/RichTiTAN/CrimsonOnion/main/version.json", token);
                    response.EnsureSuccessStatusCode();
                    var raw = await response.Content.ReadAsStringAsync(token);
                    var json = JObject.Parse(raw);
                    var remoteVer = json["version"]?.ToString() ?? "0.0.0";
                    var remoteMin = json["minAutoUpdateVersion"]?.ToString() ?? "0.0.0";

                    if (Version.Parse(remoteVer) <= Version.Parse(AppVersion))
                    {
                        Dispatcher.Invoke(() => { lblTitleText.Text = $"CrimsonOnion v{AppVersion}"; ShowToast("You are already on the latest version!", success: true); });
                        return;
                    }

                    // Check min auto-update version
                    if (Version.Parse(AppVersion) < Version.Parse(remoteMin))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            lblTitleText.Text = $"MANUAL UPDATE REQUIRED ({remoteVer})";
                            lblTitleText.Foreground = BrRed;
                            System.Windows.MessageBox.Show(
                                $"A major update (v{remoteVer}) is available!\n\nYour current version ({AppVersion}) is too old to update automatically.\n\nPlease download the latest release manually from GitHub.",
                                "Manual Update Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                            using (Process.Start(new ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true })) { }
                            ;
                        });
                        return;
                    }

                    var answer = Dispatcher.Invoke(() =>
                    {
                        lblTitleText.Text = $"CrimsonOnion v{AppVersion} - UPDATE AVAILABLE ({remoteVer})";
                        return System.Windows.MessageBox.Show($"Version {remoteVer} is available! Update now?",
                            "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    });
                    if (answer != MessageBoxResult.Yes)
                    {
                        Dispatcher.Invoke(() => lblTitleText.Text = $"CrimsonOnion v{AppVersion}");
                        return;
                    }

                    Dispatcher.Invoke(() => lblTitleText.Text = "DOWNLOADING UPDATE... 0% (CLICK TO CANCEL)");

                    var zipUrl  = "https://github.com/RichTiTAN/CrimsonOnion/releases/latest/download/CrimsonOnion.zip";
                    var zipPath = Path.Combine(_cfg.BaseDir, "update_temp.zip");
                    var extPath = Path.Combine(_cfg.BaseDir, "update_extracted");
                    if (Directory.Exists(extPath)) Directory.Delete(extPath, true);

                    using var dlClient = new HttpClient();
                    using var dlResponse = await dlClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, token);
                    dlResponse.EnsureSuccessStatusCode();
                    var total   = dlResponse.Content.Headers.ContentLength ?? -1L;
                    using var fs     = File.Create(zipPath);
                    using var stream = await dlResponse.Content.ReadAsStreamAsync(token);
                    var buffer = new byte[81920];
                    long downloaded = 0;
                    int  read;
                    int  lastPct = -1;
                    while ((read = await stream.ReadAsync(buffer, token)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read), token);
                        downloaded += read;
                        if (total > 0)
                        {
                            int pct = (int)(downloaded * 100 / total);
                            if (pct != lastPct)
                            {
                                lastPct = pct;
                                 _ = Dispatcher.InvokeAsync(() => lblTitleText.Text = $"DOWNLOADING UPDATE... {pct}% (CLICK TO CANCEL)");
                            }
                        }
                    }
                    fs.Close();

                    Dispatcher.Invoke(() => lblTitleText.Text = "EXTRACTING UPDATE...");
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extPath, true);

                    var exeFile = Directory.GetFiles(extPath, "CrimsonOnion.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (exeFile == null) throw new Exception("CrimsonOnion.exe not found in the downloaded ZIP!");

                    var sourceDir  = Path.GetDirectoryName(exeFile)!;
                    var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    var cmdArgs    = $"/c ping 127.0.0.1 -n 4 > nul & xcopy /Y /E /H /C /I \"{sourceDir}\\*\" \"{_cfg.BaseDir}\" & rmdir /S /Q \"{extPath}\" & del /Q \"{zipPath}\" & start \"\" \"{currentExe}\"";

                    Dispatcher.Invoke(() =>
                    {
                        ProxyService.DisableSystemProxy();
                        StopAllEngines(true);
                        using (Process.Start(new ProcessStartInfo("cmd.exe", cmdArgs) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true }))
                        {
                        }
                        System.Windows.Application.Current.Shutdown();
                    });
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        lblTitleText.Text = $"CrimsonOnion v{AppVersion}";
                        ShowToast("Update cancelled.");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show($"Update failed.\n\n{ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        lblTitleText.Text = $"CrimsonOnion v{AppVersion}";
                    });
                }
                finally
                {
                    _updateCts?.Dispose();
                    _updateCts = null;
                }
            });
        }

        // WINDOW LIFECYCLE
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_state.AppInitialized) return;
            _state.AppInitialized = true;

            // Clip border
            var clipGeom = new RectangleGeometry { RadiusX = 8, RadiusY = 8 };
            if (borderClip != null)
            {
                clipGeom.Rect   = new Rect(0, 0, borderClip.ActualWidth, borderClip.ActualHeight);
                borderClip.Clip = clipGeom;
            }

            // Splash fade out after 900ms hold
            var splashHold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            splashHold.Tick += (s, ev) =>
            {
                splashHold.Stop();
                var fadeDur  = new Duration(TimeSpan.FromMilliseconds(650));
                var fadeAnim = new DoubleAnimation(0, fadeDur)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                fadeAnim.Completed += (s2, e2) =>
                {
                    splashOverlay.Visibility       = Visibility.Collapsed;
                    splashOverlay.IsHitTestVisible = false;
                    if (_state.IsLogsOpen) UpdateWindowSize();
                };
                splashOverlay.BeginAnimation(OpacityProperty, fadeAnim);
                if (windowOutline != null)
                {
                    var outlineAnim = new DoubleAnimation(1, fadeDur)
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    };
                    windowOutline.BeginAnimation(OpacityProperty, outlineAnim);
                }
            };
            splashHold.Start();

            // Background tasks
            CheckUpdateSilent();

            if (_cfg.AutoStart)
            {
                var animT = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                animT.Tick += (s, ev) => { animT.Stop(); SetAutoConnectState(true, true); };
                animT.Start();
            }

            if (_cfg.AutoStart && !_state.IsFirstLaunch)
            {
                var bootT = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                bootT.Tick += (s, ev) => { bootT.Stop(); if (!_state.AbortBoot) StartEnginesAsync(); };
                bootT.Start();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            _cfg.WindowLeft = WindowState == WindowState.Normal ? Left : RestoreBounds.Left;
            _cfg.WindowTop = WindowState == WindowState.Normal ? Top : RestoreBounds.Top;
            SaveConfig();

            if (_saveDebounceTimer?.IsEnabled == true) { _saveDebounceTimer.Stop(); SaveConfig(); }
            _statsTimer?.Stop();
            _sessionClockTimer?.Stop();
            _logTimer?.Stop();
            _logClearTimer?.Stop();
            _hideAdvTimer?.Stop();
            _hideLogTimer?.Stop();
            _pingTimer?.Stop();
            
            if (_geoCts != null) { _geoCts.Cancel(); _geoCts.Dispose(); }
            if (_updateCts != null) { _updateCts.Cancel(); _updateCts.Dispose(); }

            StopAllEngines(true);

            if (_trayIcon != null)
            {
                _trayIcon.Icon?.Dispose();
                _trayIcon.Dispose();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        // HELPERS
        private void UpdateLanIp()
        {
            try
            {
                var ip = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                 !ua.Address.ToString().StartsWith("127.") &&
                                 !ua.Address.ToString().StartsWith("169.254."))
                    .Select(ua => ua.Address.ToString())
                    .FirstOrDefault();
                _state.LanIp = ip ?? "UNKNOWN";
            }
            catch { _state.LanIp = "UNKNOWN"; }
        }

        private void KillManagedProcess(string name)
        {
            var paths = new[]
            {
                GetAppPath(@"Data\Xray\xray.exe"),
                GetAppPath(@"Data\HAproxy\haproxy.exe"),
                GetAppPath(@"Data\sing_box\sing-box.exe"),
                GetAppPath(@"Data\TorBin\tor.exe")
            };
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    using (p)
                    {
                        try
                        {
                            var exePath = p.MainModule?.FileName ?? "";
                            if (paths.Any(path => string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase)) || exePath.IndexOf(@"Data\Tors\", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                p.Kill();
                                p.WaitForExit(1000);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void KillPidRef(ref int? pid)
        {
            if (pid == null) return;
            try 
            { 
                using (var p = Process.GetProcessById(pid.Value))
                {
                    p.Kill();
                    p.WaitForExit(1000);
                }
            }
            catch { }
            pid = null;
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // Protobuf decoders for raw gRPC stats
        private static int ReadVarint(byte[] data, ref int p)
        {
            int result = 0;
            int shift = 0;
            while (p < data.Length) {
                byte b = data[p++];
                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
            return result;
        }
        private static long ReadVarint64(byte[] data, ref int p)
        {
            long result = 0;
            int shift = 0;
            while (p < data.Length) {
                byte b = data[p++];
                result |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
            return result;
        }

        private async void SetSubText(string newText, SolidColorBrush? newColor = null)
        {
            if (newColor != null) btnActionSubText.Foreground = newColor;
            if (btnActionSubText.Text == newText) return;

            if (string.IsNullOrEmpty(newText))
            {
                var fadeOut = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(150)));
                EventHandler? handler = null;
                handler = (s, e) =>
                {
                    fadeOut.Completed -= handler;
                    btnActionSubText.Text = "";
                };
                fadeOut.Completed += handler;
                btnActionSubText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            else if (string.IsNullOrEmpty(btnActionSubText.Text))
            {
                await Task.Delay(150);
                if (btnActionSubText == null) return;
                btnActionSubText.Text = newText;
                btnActionSubText.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150))));
            }
            else
            {
                btnActionSubText.BeginAnimation(UIElement.OpacityProperty, null);
                btnActionSubText.Opacity = 1.0;
                btnActionSubText.Text = newText;
            }
        }

        private void ChangeTextWithFade(TextBlock tb, string newText, SolidColorBrush? newColor = null)
        {
            if (tb.Text == newText)
            {
                if (newColor != null) tb.Foreground = newColor;
                return;
            }

            var fadeOut = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(150)));
            EventHandler? handler = null;
            handler = (s, e) =>
            {
                fadeOut.Completed -= handler;
                tb.Text = newText;
                if (newColor != null) tb.Foreground = newColor;
                tb.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, new Duration(TimeSpan.FromMilliseconds(150))));
            };
            fadeOut.Completed += handler;
            tb.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
