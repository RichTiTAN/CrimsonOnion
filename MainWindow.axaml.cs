using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Input.Platform;
using CrimsonOnion.Models;
using CrimsonOnion.Services;
using CrimsonOnion.Localization;

namespace CrimsonOnion;

public class CountryItem
{
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public Bitmap? Flag { get; set; }
}

public partial class MainWindow : Window
{
    private AppConfig _cfg;
    private AppState _state;
    private List<TorControlClient> _torControlClients = new();
    private int?[] _torPids = new int?[8];
    private int? _xrayDebugPid, _sbDebugPid, _xrayDohPid;
    private DispatcherTimer? _bootstrapTimer;
    private int _pollSelCount = 6;
    private string _pollMode = "Proxy Mode";
    private string _pollSelBridge = "Direct";

    private string _activeBridge = "Direct";
    private int _activeTorEngines = 6;

    private string _tempDomains = "";
    private string _tempApps = "";
    private string _tempBlock = "";

    private static Bitmap LoadFlag(string code)
        => new Bitmap(AssetLoader.Open(new Uri($"avares://CrimsonOnion/Assets/Flags/{code}.png")));

    public List<CountryItem> Countries { get; } = new()
    {
        new() { Name = "Argentina",            Tag = "ar", Flag = LoadFlag("ar") },
        new() { Name = "Australia",            Tag = "au", Flag = LoadFlag("au") },
        new() { Name = "Austria",              Tag = "at", Flag = LoadFlag("at") },
        new() { Name = "Brazil",               Tag = "br", Flag = LoadFlag("br") },
        new() { Name = "Canada",               Tag = "ca", Flag = LoadFlag("ca") },
        new() { Name = "Finland",              Tag = "fi", Flag = LoadFlag("fi") },
        new() { Name = "France",               Tag = "fr", Flag = LoadFlag("fr") },
        new() { Name = "Germany",              Tag = "de", Flag = LoadFlag("de") },
        new() { Name = "Hong Kong",            Tag = "hk", Flag = LoadFlag("hk") },
        new() { Name = "Iceland",              Tag = "is", Flag = LoadFlag("is") },
        new() { Name = "India",                Tag = "in", Flag = LoadFlag("in") },
        new() { Name = "Italy",                Tag = "it", Flag = LoadFlag("it") },
        new() { Name = "Japan",                Tag = "jp", Flag = LoadFlag("jp") },
        new() { Name = "Mexico",               Tag = "mx", Flag = LoadFlag("mx") },
        new() { Name = "Netherlands",          Tag = "nl", Flag = LoadFlag("nl") },
        new() { Name = "New Zealand",          Tag = "nz", Flag = LoadFlag("nz") },
        new() { Name = "Romania",              Tag = "ro", Flag = LoadFlag("ro") },
        new() { Name = "Singapore",            Tag = "sg", Flag = LoadFlag("sg") },
        new() { Name = "South Africa",         Tag = "za", Flag = LoadFlag("za") },
        new() { Name = "South Korea",          Tag = "kr", Flag = LoadFlag("kr") },
        new() { Name = "Spain",                Tag = "es", Flag = LoadFlag("es") },
        new() { Name = "Sweden",               Tag = "se", Flag = LoadFlag("se") },
        new() { Name = "Switzerland",          Tag = "ch", Flag = LoadFlag("ch") },
        new() { Name = "United Arab Emirates", Tag = "ae", Flag = LoadFlag("ae") },
        new() { Name = "United Kingdom",       Tag = "gb", Flag = LoadFlag("gb") },
        new() { Name = "United States",        Tag = "us", Flag = LoadFlag("us") }
    };


    internal Models.AppState GetState() => _state;
    internal void ConnectDisconnect() => btnConnect_Click(null, new global::Avalonia.Interactivity.RoutedEventArgs());
    internal string GetSpeedText()
    {
        var down = this.FindControl<global::Avalonia.Controls.TextBlock>("lblDownloadSpeed")?.Text ?? "0 KB/s";
        var up = this.FindControl<global::Avalonia.Controls.TextBlock>("lblUploadSpeed")?.Text ?? "0 KB/s";
        var total = this.FindControl<global::Avalonia.Controls.TextBlock>("lblTotalData")?.Text ?? "0 MB";
        return $"Speed: {down}\nTotal: {total}";
    }

    public MainWindow()
    {
        InitializeComponent();
        
        var lblVer = this.FindControl<global::Avalonia.Controls.TextBlock>("lblVersion");
        if (lblVer != null) lblVer.Text = Services.UpdateService.AppVersion;

        DataContext = this;

        _cfg   = new AppConfig();
        _state = new AppState();

        _cfg.BaseDir = AppContext.BaseDirectory.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);
        _cfg.CfgFile = System.IO.Path.Combine(_cfg.BaseDir, @"Data\multiplexer_settings.json");
        _cfg.XrayDir = System.IO.Path.Combine(_cfg.BaseDir, @"Data\Xray");
        _cfg.HaPath  = System.IO.Path.Combine(_cfg.BaseDir, @"Data\HAproxy");
        _cfg.SbDir   = System.IO.Path.Combine(_cfg.BaseDir, @"Data\sing_box");

        ConfigService.Load(_cfg, _state, _cfg.CfgFile);

        _activeBridge = string.IsNullOrEmpty(_cfg.LastBridge) ? "Direct" : _cfg.LastBridge;
        if (int.TryParse(_cfg.LastCount, out int cnt)) _activeTorEngines = cnt;
        _pollMode = _cfg.LastXrayMode ?? "Proxy Mode";

        ApplyLoadedSettings();

        InitTrayIcon();
        InitLogClearTimer();

        if (!double.IsNaN(_cfg.WindowLeft) && !double.IsNaN(_cfg.WindowTop))
        {
            WindowStartupLocation = global::Avalonia.Controls.WindowStartupLocation.Manual;
            Position = new global::Avalonia.PixelPoint((int)_cfg.WindowLeft, (int)_cfg.WindowTop);
        }

        if (_cfg.StartMinimized)
        {
            WindowState = global::Avalonia.Controls.WindowState.Minimized;
        }

        bool isFirstOpen = true;
        this.Opened += (s, e) =>
        {
            if (isFirstOpen && _cfg.StartMinimized)
            {
                WindowState = global::Avalonia.Controls.WindowState.Minimized;
                if (_cfg.MinimizeToTray)
                {
                    Hide();
                }
            }
            isFirstOpen = false;
        };

        if (_cfg.AutoStart && !_state.IsFirstLaunch)
        {
            var bootT = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            bootT.Tick += (s, ev) => 
            { 
                bootT.Stop(); 
                if (!_state.AbortBoot) 
                    btnConnect_Click(null, new global::Avalonia.Interactivity.RoutedEventArgs()); 
            };
            bootT.Start();
        }

        CheckUpdateSilent();
    }


    private bool _isAnimatingPopup = false;

    private async Task ClosePopupAnimatedAsync()
    {
        if (_isAnimatingPopup) return;
        _isAnimatingPopup = true;

        var border = CountriesPopup?.Child as Border;
        if (border != null) border.Classes.Remove("popupOpen");

        var langBorder = LanguagePopup?.Child as Border;
        if (langBorder != null) langBorder.Classes.Remove("popupOpen");

        if (border != null || langBorder != null) await Task.Delay(200);

        if (CountriesPopup != null) CountriesPopup.IsOpen = false;
        if (LanguagePopup != null) LanguagePopup.IsOpen = false;
        
        var sld = this.FindControl<Border>("SettingsLightDismiss");
        if (sld != null) sld.IsVisible = false;
        
        var panSettings = this.FindControl<Border>("panSettingsOverlay");
        var panSplit = this.FindControl<Border>("panSplitOverlay");
        var panExpert = this.FindControl<Border>("panExpertOverlay");
        var panAbout = this.FindControl<Border>("panAboutOverlay");
        if ((panSettings == null || !panSettings.IsVisible) &&
            (panSplit == null || !panSplit.IsVisible) &&
            (panExpert == null || !panExpert.IsVisible) &&
            (panAbout == null || !panAbout.IsVisible))
        {
            LightDismissOverlay.IsVisible = false;
        }
        
        _isAnimatingPopup                 = false;
    }

    private void CloseAllOverlays()
    {
        var panSplitOverlay = this.FindControl<Border>("panSplitOverlay");
        if (panSplitOverlay != null && panSplitOverlay.IsVisible)
        {
            panSplitOverlay.Classes.Remove("popupOpen");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panSplitOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
            ApplyRoutingUI();
        }
        var panSettingsOverlay = this.FindControl<Border>("panSettingsOverlay");
        if (panSettingsOverlay != null && panSettingsOverlay.IsVisible)
        {
            panSettingsOverlay.Classes.Remove("popupOpen");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panSettingsOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        var panExpertOverlay = this.FindControl<Border>("panExpertOverlay");
        if (panExpertOverlay != null && panExpertOverlay.IsVisible)
        {
            panExpertOverlay.Classes.Remove("popupOpen");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panExpertOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        var panAboutOverlay = this.FindControl<Border>("panAboutOverlay");
        if (panAboutOverlay != null && panAboutOverlay.IsVisible)
        {
            panAboutOverlay.Classes.Remove("popupOpen");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panAboutOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        
        var ldo = this.FindControl<Border>("LightDismissOverlay");
        if (ldo != null) ldo.IsVisible = false;
        
        if ((CountriesPopup != null && CountriesPopup.IsOpen) || (LanguagePopup != null && LanguagePopup.IsOpen))
        {
            _ = ClosePopupAnimatedAsync();
        }
    }

    private void LightDismissOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseAllOverlays();
    }

    private void CloseOverlay_Click(object? sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
    }

    private void SidebarConnection_Click(object? sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
    }

    private void SidebarAbout_Click(object? sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
        var ldo = this.FindControl<Border>("LightDismissOverlay");
        if (ldo != null) ldo.IsVisible = true;
        
        var pan = this.FindControl<Border>("panAboutOverlay");
        if (pan != null)
        {
            pan.IsVisible = true;
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { pan.Classes.Add("popupOpen"); }, TimeSpan.FromMilliseconds(10));
        }
    }

    private void BtnGithub_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/RichTiTAN") { UseShellExecute = true });
    }

    private void BtnTelegram_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://t.me/itsTitanVPN") { UseShellExecute = true });
    }

    private async void BtnCopyAddress_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string address)
        {
            var clipboard = global::Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(address);
                ShowToast("Address copied to clipboard!", success: true);
            }
        }
    }

    private CancellationTokenSource? _updateCts;
    private string _remoteUpdateVersion = "0.0.0";

    private async void CheckUpdateSilent()
    {
        var (remoteVer, _) = await Services.UpdateService.CheckForUpdatesAsync();
        if (remoteVer != null)
        {
            _remoteUpdateVersion = remoteVer;
            var btnTitleUpdate = this.FindControl<Button>("btnTitleUpdate");
            if (btnTitleUpdate != null) btnTitleUpdate.IsVisible = true;
        }
    }

    private async void BtnTitleUpdate_Click(object? sender, RoutedEventArgs e)
    {
        var btnTitleUpdate = this.FindControl<Button>("btnTitleUpdate");
        if (btnTitleUpdate == null) return;

        if (_updateCts != null)
        {
            _updateCts.Cancel();
            _updateCts.Dispose();
            _updateCts = null;
            return;
        }
        
        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;
        
        try
        {
            await Services.UpdateService.DownloadAndInstallUpdateAsync(_remoteUpdateVersion, _cfg.BaseDir, (status) => 
            {
                btnTitleUpdate.Content = status;
            }, token);

            ProxyService.DisableSystemProxy();
            StopAllEngines(true);
            System.Environment.Exit(0);
        }
        catch (OperationCanceledException)
        {
            ShowToast("Update cancelled.");
            btnTitleUpdate.Content = "NEW UPDATE AVAILABLE";
        }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to update: {ex.Message}", "Update Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                btnTitleUpdate.Content = "NEW UPDATE AVAILABLE";
            }
        finally
        {
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    private async void BtnCheckUpdate_Click(object? sender, RoutedEventArgs e)
    {
        var btnCheckUpdate = this.FindControl<Button>("btnCheckUpdate");
        if (btnCheckUpdate == null) return;

        if (_updateCts != null)
        {
            _updateCts.Cancel();
            _updateCts.Dispose();
            _updateCts = null;
            return;
        }

        btnCheckUpdate.Content = "CHECKING FOR UPDATES...";
        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;

        try
        {
            var (remoteVer, remoteMin) = await Services.UpdateService.CheckForUpdatesAsync(token);
            if (remoteVer == null)
            {
                ShowToast("You are already on the latest version!", success: true);
                btnCheckUpdate.Content = "LATEST VERSION INSTALLED";
                await Task.Delay(3000, token);
                btnCheckUpdate.Content = "CHECK FOR UPDATES";
                _updateCts?.Dispose();
                _updateCts = null;
                return;
            }

            if (Version.Parse(Services.UpdateService.AppVersion) < Version.Parse(remoteMin ?? "0.0.0"))
            {
                btnCheckUpdate.Content = "MANUAL UPDATE REQUIRED";
                System.Windows.Forms.MessageBox.Show(
                    $"A major update (v{remoteVer}) is available!\n\nYour current version ({Services.UpdateService.AppVersion}) is too old to update automatically.\n\nPlease download the latest release manually from GitHub.", 
                    "Update Required", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                Process.Start(new ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true });
                _updateCts?.Dispose();
                _updateCts = null;
                return;
            }

            var result = System.Windows.Forms.MessageBox.Show($"Version {remoteVer} is available! Update now?", "Update Available", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Information);
            
            if (result != System.Windows.Forms.DialogResult.Yes)
            {
                btnCheckUpdate.Content = "CHECK FOR UPDATES";
                _updateCts?.Dispose();
                _updateCts = null;
                return;
            }

            await Services.UpdateService.DownloadAndInstallUpdateAsync(remoteVer, _cfg.BaseDir, (status) => 
            {
                btnCheckUpdate.Content = status;
            }, token);
            
            ProxyService.DisableSystemProxy();
            StopAllEngines(true);
            System.Environment.Exit(0);
        }
        catch (OperationCanceledException)
        {
            ShowToast("Update cancelled.");
            btnCheckUpdate.Content = "UPDATE CANCELLED";
            await Task.Delay(2000);
            btnCheckUpdate.Content = "CHECK FOR UPDATES";
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Failed to update: {ex.Message}", "Update Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            btnCheckUpdate.Content = "CHECK FOR UPDATES";
        }
        finally
        {
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    private async void BtnLanguage_Click(object? sender, RoutedEventArgs e)
    {
        if (CountriesPopup != null && CountriesPopup.IsOpen) await ClosePopupAnimatedAsync();

        if (LanguagePopup != null)
        {
            if (LanguagePopup.IsOpen && LanguagePopup.PlacementTarget?.Name == "btnLanguage")
            {
                await ClosePopupAnimatedAsync();
            }
            else
            {
                if (LanguagePopup.IsOpen) LanguagePopup.IsOpen = false;
                LanguagePopup.PlacementTarget  = this.FindControl<Control>("btnLanguage");
                LanguagePopup.Placement        = PlacementMode.Bottom;
                LanguagePopup.HorizontalOffset = 0;
                LanguagePopup.VerticalOffset   = 5;
                LanguagePopup.IsOpen           = true;
                
                var sld = this.FindControl<Border>("SettingsLightDismiss");
                if (sld != null) sld.IsVisible = true;
                
                if (LanguagePopup.Child is Border border)
                {
                    global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { border.Classes.Add("popupOpen"); }, TimeSpan.FromMilliseconds(10));
                }
            }
        }
    }

    private void LanguageOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string lang)
        {
            var lbl = this.FindControl<TextBlock>("lblCurrentLanguage");
            if (lbl != null) lbl.Text = lang;

            _cfg.Language = lang;
            SaveConfig();
            ApplyLanguage();

            _ = ClosePopupAnimatedAsync();
        }
    }

    private void SettingsLightDismiss_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (LanguagePopup != null && LanguagePopup.IsOpen)
        {
            _ = ClosePopupAnimatedAsync();
        }
    }


    private void ApplyLanguage()
    {
        AppStrings.SetLanguage(_cfg.Language);
        bool fa = AppStrings.IsPersian;

        TextBlock? F(string name) => this.FindControl<TextBlock>(name);
        Button? B(string name)    => this.FindControl<Button>(name);

        AppStrings.Apply(F("lblSidebarConnection"),  AppStrings.SidebarConnection,  AppStrings.SidebarConnection);
        AppStrings.Apply(F("lblSidebarCountries"),   AppStrings.SidebarCountries,   AppStrings.SidebarCountries);
        AppStrings.Apply(F("lblSidebarSplitTunnel"), AppStrings.SidebarSplitTunnel, AppStrings.SidebarSplitTunnel);
        AppStrings.Apply(F("lblSidebarSettings"),    AppStrings.SidebarSettings,    AppStrings.SidebarSettings);
        AppStrings.Apply(F("lblSidebarAbout"),       AppStrings.SidebarAbout,       AppStrings.SidebarAbout);

        AppStrings.Apply(F("lblProxyMode"),  AppStrings.ProxyMode,  AppStrings.ProxyMode);
        AppStrings.Apply(F("lblVpnMode"),    AppStrings.VpnMode,    AppStrings.VpnMode);
        AppStrings.Apply(F("lblClearProxy"), AppStrings.ClearProxy, AppStrings.ClearProxy);

        AppStrings.ApplyToolTip(B("btnProxyMode"),  AppStrings.TtProxyMode,  AppStrings.TtProxyMode);
        AppStrings.ApplyToolTip(B("btnVpnMode"),    AppStrings.TtVpnMode,    AppStrings.TtVpnMode);
        AppStrings.ApplyToolTip(B("btnClearProxy"), AppStrings.TtClearProxy, AppStrings.TtClearProxy);

        
        var panTimerContent = this.FindControl<StackPanel>("panTimerContent");
        if (panTimerContent != null)
        {
            panTimerContent.FlowDirection = AppStrings.IsPersian 
                ? global::Avalonia.Media.FlowDirection.RightToLeft 
                : global::Avalonia.Media.FlowDirection.LeftToRight;
        }

        AppStrings.Apply(F("lblBridgeType"),    AppStrings.BridgeType,    AppStrings.BridgeType);
        AppStrings.Apply(F("lblGetBridges"),    AppStrings.GetBridges,    AppStrings.GetBridges);
        AppStrings.Apply(F("lblTorEngines"),    AppStrings.TorEngines,    AppStrings.TorEngines);
        AppStrings.Apply(F("lblLogsStatus"),    AppStrings.LogsStatus,    AppStrings.LogsStatus);
        AppStrings.Apply(F("lblTorBootstrap"),  AppStrings.TorBootstrap,  AppStrings.TorBootstrap);
        AppStrings.Apply(F("lblXrayLogHeader"), AppStrings.XrayLogHeader, AppStrings.XrayLogHeader);
        AppStrings.Apply(F("lblConnectedFor"),  AppStrings.ConnectedFor,  AppStrings.ConnectedFor);
        AppStrings.Apply(F("lblConnectedTo"),   AppStrings.ConnectedTo,   AppStrings.ConnectedTo);
        AppStrings.Apply(F("lblDisconnected"),  AppStrings.Disconnected,  AppStrings.Disconnected);
        AppStrings.Apply(F("lblLocalPortLabel"),AppStrings.OpenLocalPort, AppStrings.OpenLocalPort);
        AppStrings.Apply(F("lblLanPortLabel"),  AppStrings.OpenLanPort,   AppStrings.OpenLanPort);
        AppStrings.Apply(F("lblPingLabel"),     AppStrings.PingLabel,     AppStrings.PingLabel);
        AppStrings.Apply(F("lblTotalLabel"),    AppStrings.TotalLabel,    AppStrings.TotalLabel);
        AppStrings.Apply(F("lblDownloadLabel"), AppStrings.DownloadLabel, AppStrings.DownloadLabel);
        AppStrings.Apply(F("lblUploadLabel"),   AppStrings.UploadLabel,   AppStrings.UploadLabel);

        var btnConn = this.FindControl<Button>("btnConnect");
        if (btnConn != null)
        {
            var txt = this.FindControl<TextBlock>("txtConnectBtn");
            if (txt != null)
            {
                bool connected = _state.IsConnected;
                txt.Text = connected
                    ? (fa ? "قطع اتصال" : "DISCONNECT")
                    : (fa ? "اتصال"     : "CONNECT");
                txt.FlowDirection = fa
                    ? global::Avalonia.Media.FlowDirection.RightToLeft
                    : global::Avalonia.Media.FlowDirection.LeftToRight;
            }
        }

        AppStrings.Apply(F("lblSectionStartup"),    AppStrings.SectionStartup,   AppStrings.SectionStartup, true);
        AppStrings.Apply(F("lblLaunchOnStartup"),  AppStrings.LaunchOnStartup,  AppStrings.LaunchOnStartup);
        AppStrings.Apply(F("lblAutoConnect"),       AppStrings.AutoConnect,      AppStrings.AutoConnect);
        AppStrings.Apply(F("lblStartMinimized"),    AppStrings.StartMinimized,   AppStrings.StartMinimized);
        AppStrings.Apply(F("lblMinimizeToTray"),    AppStrings.MinimizeToTray,   AppStrings.MinimizeToTray);

        AppStrings.Apply(F("lblSectionConnection"),  AppStrings.SectionConnection, AppStrings.SectionConnection, true);


        var tbCustomXray = this.FindControl<TextBlock>("lblCustomXrayExit");
        AppStrings.Apply(tbCustomXray, AppStrings.CustomXrayExit, AppStrings.CustomXrayExit);
        AppStrings.ApplyToolTip(tbCustomXray, AppStrings.TtCustomXray, AppStrings.TtCustomXray);
        
        var tbOutboundProxy = this.FindControl<TextBlock>("lblOutboundProxySetting");
        AppStrings.Apply(tbOutboundProxy, AppStrings.OutboundProxy, AppStrings.OutboundProxy);
        AppStrings.ApplyToolTip(tbOutboundProxy, AppStrings.TtOutboundProxy, AppStrings.TtOutboundProxy);
        
        var tbDnsSetting = this.FindControl<TextBlock>("lblDnsSettingTitle");
        AppStrings.Apply(tbDnsSetting, AppStrings.DnsSettings, AppStrings.DnsSettings);
        AppStrings.ApplyToolTip(tbDnsSetting, AppStrings.TtDnsSettings, AppStrings.TtDnsSettings);

        var tbAdBlocker = this.FindControl<TextBlock>("lblAdBlockerSetting");
        AppStrings.Apply(tbAdBlocker, AppStrings.AdBlocker, AppStrings.AdBlocker);
        AppStrings.ApplyToolTip(tbAdBlocker, AppStrings.TtAdBlocker, AppStrings.TtAdBlocker);
        
        var tbAllowLan = this.FindControl<TextBlock>("lblAllowLanSetting");
        AppStrings.Apply(tbAllowLan, AppStrings.AllowLan, AppStrings.AllowLan);
        AppStrings.ApplyToolTip(tbAllowLan, AppStrings.TtAllowLan, AppStrings.TtAllowLan);

        AppStrings.Apply(F("lblOutboundType"),     AppStrings.ProxyType,      AppStrings.ProxyType);
        AppStrings.Apply(F("lblOutboundAddress"),  AppStrings.AddressIp,      AppStrings.AddressIp);
        AppStrings.Apply(F("lblOutboundPort"),     AppStrings.Port,           AppStrings.Port);
        AppStrings.Apply(F("lblOutboundAuth"),     AppStrings.Authentication, AppStrings.Authentication);
        AppStrings.Apply(F("lblOutboundUsername"), AppStrings.Username,       AppStrings.Username);
        AppStrings.Apply(F("lblOutboundPassword"), AppStrings.Password,       AppStrings.Password);
        AppStrings.Apply(F("lblUpstreamDoh"),      AppStrings.UpstreamDohUrl, AppStrings.UpstreamDohUrl);

        AppStrings.Apply(F("lblSectionSystem"),    AppStrings.SectionSystem,    AppStrings.SectionSystem, true);
        
        var tbLanguageSetting = this.FindControl<TextBlock>("lblLanguageSetting");
        AppStrings.Apply(tbLanguageSetting, AppStrings.LanguageSetting, AppStrings.LanguageSetting);
        AppStrings.ApplyToolTip(tbLanguageSetting, AppStrings.TtLanguage, AppStrings.TtLanguage);
        
        var tbDebugMode = this.FindControl<TextBlock>("lblDebugMode");
        AppStrings.Apply(tbDebugMode, AppStrings.DebugMode, AppStrings.DebugMode);
        AppStrings.ApplyToolTip(tbDebugMode, AppStrings.TtDebugMode, AppStrings.TtDebugMode);
        
        AppStrings.Apply(F("lblDesktopShortcut"),  AppStrings.DesktopShortcut,  AppStrings.DesktopShortcut);
        AppStrings.Apply(F("lblStartMenuShortcut"),AppStrings.StartMenuShortcut,AppStrings.StartMenuShortcut);

        AppStrings.ApplyBtn(B("btnDesktopShortcut"),   AppStrings.Create, AppStrings.Create);
        AppStrings.ApplyBtn(B("btnStartMenuShortcut"), AppStrings.Create, AppStrings.Create);

        AppStrings.Apply(F("lblSplitTunnelingHeader"), AppStrings.SplitTunneling, AppStrings.SplitTunneling, true);
        AppStrings.Apply(F("lblDomainsAndIps"),         AppStrings.DomainsAndIps,  AppStrings.DomainsAndIps);
        AppStrings.Apply(F("lblApplications"),          AppStrings.Applications,   AppStrings.Applications);
        AppStrings.Apply(F("lblBlockedDomainsIps"),     AppStrings.BlockedDomains, AppStrings.BlockedDomains);
        
        UpdateSplitTunnelUI();

        var btnSplitDisabled  = this.FindControl<Button>("btnSplitDisabled");
        var btnSplitExclusive = this.FindControl<Button>("btnSplitExclusive");
        var btnSplitInclusive = this.FindControl<Button>("btnSplitInclusive");
        
        AppStrings.ApplyToolTip(btnSplitDisabled, AppStrings.TtSplitDis, AppStrings.TtSplitDis);
        AppStrings.ApplyToolTip(btnSplitExclusive, AppStrings.TtSplitExc, AppStrings.TtSplitExc);
        AppStrings.ApplyToolTip(btnSplitInclusive, AppStrings.TtSplitInc, AppStrings.TtSplitInc);
        
        if (btnSplitDisabled?.Content  is TextBlock tbDis) AppStrings.Apply(tbDis, AppStrings.Disabled,  AppStrings.Disabled);
        if (btnSplitExclusive?.Content is TextBlock tbEx)  AppStrings.Apply(tbEx,  AppStrings.Exclusive, AppStrings.Exclusive);
        if (btnSplitInclusive?.Content is TextBlock tbIn)  AppStrings.Apply(tbIn,  AppStrings.Inclusive, AppStrings.Inclusive);

        var btnToggleDomains = this.FindControl<Button>("btnToggleDomains");
        var btnToggleApps    = this.FindControl<Button>("btnToggleApps");
        var btnToggleBlock   = this.FindControl<Button>("btnToggleBlock");
        if (btnToggleDomains != null) 
            btnToggleDomains.Content = string.IsNullOrWhiteSpace(this.FindControl<TextBox>("txtSplitDomains")?.Text) ? AppStrings.Add : AppStrings.Edit;
        if (btnToggleApps    != null) 
            btnToggleApps.Content    = string.IsNullOrWhiteSpace(this.FindControl<TextBox>("txtSplitApps")?.Text) ? AppStrings.Add : AppStrings.Edit;
        if (btnToggleBlock   != null) 
            btnToggleBlock.Content   = string.IsNullOrWhiteSpace(this.FindControl<TextBox>("txtSplitBlock")?.Text) ? AppStrings.Add : AppStrings.Edit;

        var btnBrowseApp = this.FindControl<Button>("btnBrowseApp");
        if (btnBrowseApp != null) btnBrowseApp.Content = fa ? "مرور" : "BROWSE";

        AppStrings.Apply(F("lblAboutVersion"),  AppStrings.AboutVersion,  AppStrings.AboutVersion);
        AppStrings.Apply(F("lblDonations"),     AppStrings.Donations,     AppStrings.Donations);
        AppStrings.Apply(F("lblDonationsDesc"), AppStrings.DonationsDesc, AppStrings.DonationsDesc);
        AppStrings.ApplyBtn(B("btnCheckUpdate"), AppStrings.CheckForUpdates, AppStrings.CheckForUpdates);

        AppStrings.Apply(F("lblExpertTitle"),    AppStrings.ExpertTitle,     AppStrings.ExpertTitle);
        AppStrings.Apply(F("lblHardwareAccel"),  AppStrings.HardwareAccel,   AppStrings.HardwareAccel);
        AppStrings.Apply(F("lblFascistFirewall"),AppStrings.FascistFirewall, AppStrings.FascistFirewall);
        AppStrings.Apply(F("lblStrictNodes"),    AppStrings.StrictNodes,     AppStrings.StrictNodes);
        AppStrings.Apply(F("lblCustomTorrc"),    AppStrings.CustomTorrcLabel,AppStrings.CustomTorrcLabel);

        if (_trayWidget != null)
            _trayWidget.ApplyLanguage(fa);
    }

    private async void SidebarCountries_Click(object? sender, RoutedEventArgs e)
    {
        CloseAllOverlays();

        if (CountriesPopup != null)
        {
            if (CountriesPopup.IsOpen && CountriesPopup.PlacementTarget?.Name == "SidebarBorder")
            {
                await ClosePopupAnimatedAsync();
            }
            else
            {
                if (CountriesPopup.IsOpen) CountriesPopup.IsOpen = false;
                CountriesPopup.PlacementTarget  = this.FindControl<Control>("SidebarBorder");
                CountriesPopup.Placement        = PlacementMode.RightEdgeAlignedTop;
                CountriesPopup.HorizontalOffset = 10;
                CountriesPopup.VerticalOffset   = 0;
                CountriesPopup.IsOpen           = true;
                if (CountriesPopup.Child is Border border) border.Classes.Add("popupOpen");
                LightDismissOverlay.IsVisible   = true;
            }
        }
    }

    private async void MainCountries_Click(object? sender, RoutedEventArgs e)
    {
        CloseAllOverlays();

        if (CountriesPopup != null)
        {
            if (CountriesPopup.IsOpen && CountriesPopup.PlacementTarget?.Name == "btnCurrentCountry")
            {
                await ClosePopupAnimatedAsync();
            }
            else
            {
                if (CountriesPopup.IsOpen) CountriesPopup.IsOpen = false;
                CountriesPopup.PlacementTarget  = this.FindControl<Control>("btnCurrentCountry");
                CountriesPopup.Placement        = PlacementMode.Bottom;
                CountriesPopup.HorizontalOffset = 0;
                CountriesPopup.VerticalOffset   = 5;
                CountriesPopup.IsOpen           = true;
                if (CountriesPopup.Child is Border border) border.Classes.Add("popupOpen");
                LightDismissOverlay.IsVisible   = true;
            }
        }
    }

    private void Mode_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button clickedBtn)
        {
            if (clickedBtn.Name == "btnVpnMode" && _activeBridge == "snowflake") return;

            string newMode;
            if (clickedBtn.Name == "btnProxyMode")       newMode = "Proxy Mode";
            else if (clickedBtn.Name == "btnVpnMode")    newMode = "VPN Mode";
            else if (clickedBtn.Name == "btnClearProxy") newMode = "Clear Proxy";
            else                                         newMode = "Proxy Mode";

            bool modeChanged = _cfg.LastXrayMode != newMode;
            if (modeChanged) _cfg.LastXrayMode = newMode;

            ApplyModeUI(newMode);

            if (modeChanged)
            {
                _pollMode         = newMode;
                RequestConfigSave();
                SmartRestartXray();
            }
        }
    }

    private void Bridge_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button clickedBtn)
        {
            this.FindControl<Button>("btnBridgeDirect")?.Classes.Remove("activeOpt");
            this.FindControl<Button>("btnBridgeObfs4")?.Classes.Remove("activeOpt");
            this.FindControl<Button>("btnBridgeSnowflake")?.Classes.Remove("activeOpt");
            this.FindControl<Button>("btnBridgeMeek")?.Classes.Remove("activeOpt");
            this.FindControl<Button>("btnBridgeCustom")?.Classes.Remove("activeOpt");

            clickedBtn.Classes.Add("activeOpt");

            if (clickedBtn.Name == "btnBridgeDirect")         _activeBridge = "Direct";
            else if (clickedBtn.Name == "btnBridgeObfs4")     _activeBridge = "obfs4";
            else if (clickedBtn.Name == "btnBridgeSnowflake") _activeBridge = "snowflake";
            else if (clickedBtn.Name == "btnBridgeMeek")      _activeBridge = "meek_lite";
            else if (clickedBtn.Name == "btnBridgeCustom")    _activeBridge = "Custom";

            _cfg.LastBridge = _activeBridge;
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, "Optimized", _cfg.LastBridge, _cfg.LastCount);

            if (_activeBridge == "snowflake" && _cfg.LastXrayMode == "VPN Mode")
            {
                _cfg.LastXrayMode = "Proxy Mode";
                _pollMode         = "Proxy Mode";
                ShowToast("VPN Mode disabled for Snowflake bridge.");
            }

            ApplyModeUI(_pollMode);

            var panCustomBridge = this.FindControl<Border>("panCustomBridge");
            if (panCustomBridge != null)
            {
                if (_activeBridge == "Custom")
                {
                    panCustomBridge.MaxHeight       = 500;
                    panCustomBridge.Opacity         = 1;
                    panCustomBridge.BorderThickness = new global::Avalonia.Thickness(1);
                    var txtCustomBridge = this.FindControl<TextBox>("txtCustomBridge");
                    if (txtCustomBridge != null) txtCustomBridge.Text = _cfg.CustomBridgeLine;
                }
                else
                {
                    panCustomBridge.MaxHeight       = 0;
                    panCustomBridge.Opacity         = 0;
                    panCustomBridge.BorderThickness = new global::Avalonia.Thickness(0);
                }
            }

            if (_state.IsEngineRunning)
                ShowToast("Please reconnect to apply the new bridge.");
        }
    }

    private void Engines_ValueChanged(object? sender, global::Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Slider slider)
        {
            int engines = (int)slider.Value;
            var lbl = this.FindControl<TextBlock>("lblEngineCount");
            if (lbl != null) lbl.Text = engines.ToString();

            if (_activeTorEngines == engines) return;

            _activeTorEngines = engines;
            _cfg.LastCount    = engines.ToString();

            if (_state.IsEngineRunning)
                OnEngineCountChanged(engines);
            else
                RequestConfigSave();
        }
    }


    private void ApplyLoadedSettings()
    {
        this.FindControl<global::Avalonia.Controls.Button>("btnBridgeDirect")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnBridgeObfs4")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnBridgeSnowflake")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnBridgeMeek")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnBridgeCustom")?.Classes.Remove("activeOpt");

        if (_activeBridge == "obfs4")          this.FindControl<global::Avalonia.Controls.Button>("btnBridgeObfs4")?.Classes.Add("activeOpt");
        else if (_activeBridge == "snowflake") this.FindControl<global::Avalonia.Controls.Button>("btnBridgeSnowflake")?.Classes.Add("activeOpt");
        else if (_activeBridge == "meek_lite") this.FindControl<global::Avalonia.Controls.Button>("btnBridgeMeek")?.Classes.Add("activeOpt");
        else if (_activeBridge == "Custom")    this.FindControl<global::Avalonia.Controls.Button>("btnBridgeCustom")?.Classes.Add("activeOpt");
        else                                   this.FindControl<global::Avalonia.Controls.Button>("btnBridgeDirect")?.Classes.Add("activeOpt");

        var txtCustomBridge = this.FindControl<global::Avalonia.Controls.TextBox>("txtCustomBridge");
        if (txtCustomBridge != null) txtCustomBridge.Text = _cfg.CustomBridgeLine;

        var sldEngines = this.FindControl<global::Avalonia.Controls.Slider>("sldEngines");
        if (sldEngines != null) sldEngines.Value = _activeTorEngines;
        var lblEngineCount = this.FindControl<global::Avalonia.Controls.TextBlock>("lblEngineCount");
        if (lblEngineCount != null) lblEngineCount.Text = _activeTorEngines.ToString();

        var chkLogs = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("chkLogs");
        if (chkLogs != null) chkLogs.IsChecked = _state.IsLogsOpen;

        _isInitializingSettings = true;
        
        var btnBootTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnBootTog");
        if (btnBootTog != null) btnBootTog.IsChecked = _cfg.LaunchOnBoot;
        
        var btnAutoTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnAutoTog");
        if (btnAutoTog != null) btnAutoTog.IsChecked = _cfg.AutoStart;
        
        var btnStartMinTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnStartMinTog");
        if (btnStartMinTog != null) btnStartMinTog.IsChecked = _cfg.StartMinimized;
        
        var btnTrayTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnTrayTog");
        if (btnTrayTog != null) btnTrayTog.IsChecked = _cfg.MinimizeToTray;
        
        var togDnsSettings = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togDnsSettings");
        if (togDnsSettings != null) togDnsSettings.IsChecked = _cfg.EnableUpstreamDoh;
        
        var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");
        if (cmbDohUrl != null) cmbDohUrl.Text = _cfg.UpstreamDohUrl;
        
        var btnAdBlockTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnAdBlockTog");
        if (btnAdBlockTog != null) btnAdBlockTog.IsChecked = _cfg.EnableAdBlock;
        
        var btnLanTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnLanTog");
        if (btnLanTog != null) btnLanTog.IsChecked = _cfg.AllowLanConnections;
        
        var btnDebugTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnDebugTog");
        if (btnDebugTog != null) btnDebugTog.IsChecked = _cfg.DebugMode;

        var togOutboundProxy = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundProxy");
        if (togOutboundProxy != null) togOutboundProxy.IsChecked = _cfg.EnableOutboundProxy;

        var togXrayExitNode = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togXrayExitNode");
        if (togXrayExitNode != null) togXrayExitNode.IsChecked = _cfg.EnableV2rayChain;

        var txtOutboundAddr = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundAddr");
        if (txtOutboundAddr != null) txtOutboundAddr.Text = _cfg.OutboundProxyAddress;
        var txtOutboundPort = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPort");
        if (txtOutboundPort != null) txtOutboundPort.Text = _cfg.OutboundProxyPort;
        var cmbOutboundType = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbOutboundType");
        if (cmbOutboundType != null) cmbOutboundType.SelectedIndex = _cfg.OutboundProxyType == "HTTPS" ? 1 : 0;
        var togOutboundAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundAuth");
        if (togOutboundAuth != null) togOutboundAuth.IsChecked = _cfg.EnableOutboundAuth;
        var txtOutboundUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundUser");
        if (txtOutboundUser != null) txtOutboundUser.Text = _cfg.OutboundProxyUser;
        var txtOutboundPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPass");
        if (txtOutboundPass != null) txtOutboundPass.Text = _cfg.OutboundProxyPass;
        
        _isInitializingSettings = false;

        ApplyModeUI(_pollMode);
        ApplyRoutingUI();
        UpdateLanPortUI();

        var langLbl = this.FindControl<TextBlock>("lblCurrentLanguage");
        if (langLbl != null) langLbl.Text = _cfg.Language;
        ApplyLanguage();
    }

    private void ApplyModeUI(string mode)
    {
        this.FindControl<global::Avalonia.Controls.Button>("btnProxyMode")?.Classes.Remove("activeMode");
        this.FindControl<global::Avalonia.Controls.Button>("btnVpnMode")?.Classes.Remove("activeMode");
        this.FindControl<global::Avalonia.Controls.Button>("btnClearProxy")?.Classes.Remove("activeMode");

        var btnVpnMode = this.FindControl<global::Avalonia.Controls.Button>("btnVpnMode");
        if (btnVpnMode != null)
        {
            if (_activeBridge == "snowflake")
            {
                btnVpnMode.IsEnabled = false;
                btnVpnMode.Opacity = 0.3;
            }
            else
            {
                btnVpnMode.IsEnabled = true;
                btnVpnMode.Opacity = 1.0;
            }
        }

        if (mode == "VPN Mode")       
            this.FindControl<global::Avalonia.Controls.Button>("btnVpnMode")?.Classes.Add("activeMode");
        else if (mode == "Clear Proxy") 
            this.FindControl<global::Avalonia.Controls.Button>("btnClearProxy")?.Classes.Add("activeMode");
        else                           
            this.FindControl<global::Avalonia.Controls.Button>("btnProxyMode")?.Classes.Add("activeMode");
            
        UpdateSplitTunnelUI();
    }

    private void UpdateSplitTunnelUI()
    {
        this.FindControl<global::Avalonia.Controls.Button>("btnSplitDisabled")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnSplitExclusive")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnSplitInclusive")?.Classes.Remove("activeOpt");

        var modeStr = _cfg.SplitTunnelMode ?? "DISABLED";
        if (modeStr == "EXCLUSIVE") this.FindControl<global::Avalonia.Controls.Button>("btnSplitExclusive")?.Classes.Add("activeOpt");
        else if (modeStr == "INCLUSIVE") this.FindControl<global::Avalonia.Controls.Button>("btnSplitInclusive")?.Classes.Add("activeOpt");
        else this.FindControl<global::Avalonia.Controls.Button>("btnSplitDisabled")?.Classes.Add("activeOpt");

        var panSplitConfig = this.FindControl<global::Avalonia.Controls.Border>("panSplitConfig");
        if (panSplitConfig != null)
        {
            if (modeStr != "EXCLUSIVE" && modeStr != "INCLUSIVE")
            {
                panSplitConfig.MaxHeight = 0;
                panSplitConfig.Opacity = 0;
            }
            else
            {
                panSplitConfig.MaxHeight = 800;
                panSplitConfig.Opacity = 1;
            }
        }

        var lblSplitExplanation = this.FindControl<global::Avalonia.Controls.TextBlock>("lblSplitExplanation");
        if (lblSplitExplanation != null)
        {
            if (modeStr == "EXCLUSIVE")
                lblSplitExplanation.Text = CrimsonOnion.Localization.AppStrings.SplitExplanationExclusive;
            else if (modeStr == "INCLUSIVE")
                lblSplitExplanation.Text = CrimsonOnion.Localization.AppStrings.SplitExplanationInclusive;
        }

        var panSplitDomains = this.FindControl<global::Avalonia.Controls.StackPanel>("panSplitDomains");
        var panSplitApps = this.FindControl<global::Avalonia.Controls.StackPanel>("panSplitApps");

        if (panSplitDomains != null && panSplitApps != null)
        {

            if (_cfg.LastXrayMode == "VPN Mode")
            {
                panSplitDomains.IsEnabled = false;
                panSplitDomains.Opacity = 0.3;
                
                panSplitApps.IsEnabled = true;
                panSplitApps.Opacity = 1.0;
            }
            else
            {
                panSplitDomains.IsEnabled = true;
                panSplitDomains.Opacity = 1.0;
                
                panSplitApps.IsEnabled = false;
                panSplitApps.Opacity = 0.3;
            }
        }
        
        var txtSplitDomains = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitDomains");
        if (txtSplitDomains != null)
        {
            if (txtSplitDomains.Text != _cfg.LastManualSplit) txtSplitDomains.Text = _cfg.LastManualSplit;
            InitPanel(this.FindControl<global::Avalonia.Controls.Border>("panDomainsEdit")!, this.FindControl<global::Avalonia.Controls.Border>("panDomainsToggle")!, txtSplitDomains, this.FindControl<global::Avalonia.Controls.Button>("btnToggleDomains")!);
        }
            
        var txtSplitApps = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitApps");
        if (txtSplitApps != null)
        {
            if (txtSplitApps.Text != _cfg.LastAppSplit) txtSplitApps.Text = _cfg.LastAppSplit;
            InitPanel(this.FindControl<global::Avalonia.Controls.Border>("panAppsEdit")!, this.FindControl<global::Avalonia.Controls.Border>("panAppsToggle")!, txtSplitApps, this.FindControl<global::Avalonia.Controls.Button>("btnToggleApps")!);
        }
            
        var txtSplitBlock = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitBlock");
        if (txtSplitBlock != null)
        {
            if (txtSplitBlock.Text != _cfg.LastBlockSplit) txtSplitBlock.Text = _cfg.LastBlockSplit;
            InitPanel(this.FindControl<global::Avalonia.Controls.Border>("panBlockEdit")!, this.FindControl<global::Avalonia.Controls.Border>("panBlockToggle")!, txtSplitBlock, this.FindControl<global::Avalonia.Controls.Button>("btnToggleBlock")!);
        }
    }

    private void SplitTunnel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Button clickedBtn)
        {
            if (clickedBtn.Name == "btnSplitExclusive") _cfg.SplitTunnelMode = "EXCLUSIVE";
            else if (clickedBtn.Name == "btnSplitInclusive") _cfg.SplitTunnelMode = "INCLUSIVE";
            else _cfg.SplitTunnelMode = "DISABLED";

            _cfg.EnableDirect = _cfg.SplitTunnelMode != "DISABLED";

            UpdateSplitTunnelUI();
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, "Optimized", _cfg.LastBridge, _cfg.LastCount);
            
            if (_state.IsEngineRunning)
                SmartRestartXray();
        }
    }

    private void InitPanel(Border panel, Border togglePanel, TextBox tb, Button btnToggle)
    {
        bool hasText = !string.IsNullOrWhiteSpace(tb.Text);
        panel.Height = hasText ? 34 : 0;
        btnToggle.Content = hasText ? CrimsonOnion.Localization.AppStrings.Edit : CrimsonOnion.Localization.AppStrings.Add;
        togglePanel.CornerRadius = hasText ? new global::Avalonia.CornerRadius(4, 4, 0, 0) : new global::Avalonia.CornerRadius(4);
        
        if (hasText)
        {
            tb.Height = 20;
            tb.Margin = new global::Avalonia.Thickness(0);
            tb.IsHitTestVisible = false;
            tb.IsReadOnly = true;
            tb.Focusable = false;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow);
        }
        else
        {
            tb.Height = 46;
            tb.IsHitTestVisible = true;
            tb.IsReadOnly = false;
            tb.Focusable = true;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Ibeam);
        }
    }

    private void TogglePanel(Border panel, Border togglePanel, TextBox tb, Button btnToggle, ref string tempStore)
    {
        if (panel.Height < 110)
        {
            tempStore = tb.Text ?? "";
            
            tb.Height = 56;
            tb.Margin = new global::Avalonia.Thickness(0, 5, 0, 0);
            tb.IsHitTestVisible = true;
            tb.IsReadOnly = false;
            tb.Focusable = true;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Ibeam);
            btnToggle.Content = CrimsonOnion.Localization.AppStrings.Edit;
            togglePanel.CornerRadius = new global::Avalonia.CornerRadius(4, 4, 0, 0);
            
            panel.Height = 110;
            tb.Focus();
        }
        else
        {
            ClosePanel(panel, togglePanel, tb, btnToggle);
        }
    }

    private void ClosePanel(Border panel, Border togglePanel, TextBox tb, Button btnToggle)
    {
        bool hasText = !string.IsNullOrWhiteSpace(tb.Text);
        double targetHeight = hasText ? 34 : 0;
        
        btnToggle.Content = hasText ? CrimsonOnion.Localization.AppStrings.Edit : CrimsonOnion.Localization.AppStrings.Add;
        togglePanel.CornerRadius = (hasText || targetHeight > 0) ? new global::Avalonia.CornerRadius(4, 4, 0, 0) : new global::Avalonia.CornerRadius(4);
        
        if (hasText)
        {
            tb.Height = 20;
            tb.Margin = new global::Avalonia.Thickness(0);
            tb.IsHitTestVisible = false;
            tb.IsReadOnly = true;
            tb.Focusable = false;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow);
        }
        
        panel.Height = targetHeight;
    }

    private void SplitToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Name == "btnToggleDomains") TogglePanel(this.FindControl<Border>("panDomainsEdit")!, this.FindControl<Border>("panDomainsToggle")!, this.FindControl<TextBox>("txtSplitDomains")!, btn, ref _tempDomains);
            else if (btn.Name == "btnToggleApps") TogglePanel(this.FindControl<Border>("panAppsEdit")!, this.FindControl<Border>("panAppsToggle")!, this.FindControl<TextBox>("txtSplitApps")!, btn, ref _tempApps);
            else if (btn.Name == "btnToggleBlock") TogglePanel(this.FindControl<Border>("panBlockEdit")!, this.FindControl<Border>("panBlockToggle")!, this.FindControl<TextBox>("txtSplitBlock")!, btn, ref _tempBlock);
        }
    }

    private void SplitSave_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Name == "btnSaveDomains")
            {
                var tb = this.FindControl<TextBox>("txtSplitDomains")!;
                _cfg.LastManualSplit = tb.Text?.Trim() ?? "";
                ClosePanel(this.FindControl<Border>("panDomainsEdit")!, this.FindControl<Border>("panDomainsToggle")!, tb, this.FindControl<Button>("btnToggleDomains")!);
            }
            else if (btn.Name == "btnSaveApps")
            {
                var tb = this.FindControl<TextBox>("txtSplitApps")!;
                _cfg.LastAppSplit = tb.Text?.Trim() ?? "";
                ClosePanel(this.FindControl<Border>("panAppsEdit")!, this.FindControl<Border>("panAppsToggle")!, tb, this.FindControl<Button>("btnToggleApps")!);
            }
            else if (btn.Name == "btnSaveBlock")
            {
                var tb = this.FindControl<TextBox>("txtSplitBlock")!;
                _cfg.LastBlockSplit = tb.Text?.Trim() ?? "";
                ClosePanel(this.FindControl<Border>("panBlockEdit")!, this.FindControl<Border>("panBlockToggle")!, tb, this.FindControl<Button>("btnToggleBlock")!);
            }
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, "Optimized", _cfg.LastBridge, _cfg.LastCount);
            if (_state.IsEngineRunning)
                SmartRestartXray();
        }
    }

    private void SplitCancel_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Name == "btnCancelDomains")
            {
                var tb = this.FindControl<TextBox>("txtSplitDomains")!;
                tb.Text = _tempDomains;
                ClosePanel(this.FindControl<Border>("panDomainsEdit")!, this.FindControl<Border>("panDomainsToggle")!, tb, this.FindControl<Button>("btnToggleDomains")!);
            }
            else if (btn.Name == "btnCancelApps")
            {
                var tb = this.FindControl<TextBox>("txtSplitApps")!;
                tb.Text = _tempApps;
                ClosePanel(this.FindControl<Border>("panAppsEdit")!, this.FindControl<Border>("panAppsToggle")!, tb, this.FindControl<Button>("btnToggleApps")!);
            }
            else if (btn.Name == "btnCancelBlock")
            {
                var tb = this.FindControl<TextBox>("txtSplitBlock")!;
                tb.Text = _tempBlock;
                ClosePanel(this.FindControl<Border>("panBlockEdit")!, this.FindControl<Border>("panBlockToggle")!, tb, this.FindControl<Button>("btnToggleBlock")!);
            }
        }
    }

    private async void BrowseApp_Click(object? sender, RoutedEventArgs e)
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow == null) return;
            
            var storageProvider = mainWindow.StorageProvider;
            var fileOptions = new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Application",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new global::Avalonia.Platform.Storage.FilePickerFileType("Executables") { Patterns = new[] { "*.exe" } },
                    new global::Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            };

            var result = await storageProvider.OpenFilePickerAsync(fileOptions);
            if (result != null && result.Count > 0)
            {
                var file = result[0];
                var exeName = file.Name;
                
                var txtSplitApps = this.FindControl<TextBox>("txtSplitApps");
                if (txtSplitApps != null)
                {
                    if (string.IsNullOrWhiteSpace(txtSplitApps.Text))
                        txtSplitApps.Text = exeName;
                    else if (!txtSplitApps.Text.Split(',').Any(a => a.Trim().Equals(exeName, StringComparison.OrdinalIgnoreCase)))
                        txtSplitApps.Text += $", {exeName}";
                        
                    _cfg.LastAppSplit = txtSplitApps.Text;
                    ConfigService.Save(_cfg, _state, _cfg.CfgFile, "Optimized", _cfg.LastBridge, _cfg.LastCount);
                    if (_state.IsEngineRunning)
                        SmartRestartXray();
                }
            }
        }
    }

    // ---------------------------------------------------------------------
    // ---------------------------------------------------------------------

    private void TitleBar_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_cfg.MinimizeToTray)
            Hide();
        else
            WindowState = WindowState.Minimized;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(global::Avalonia.Controls.WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (this.WindowState == global::Avalonia.Controls.WindowState.Normal)
        {
            _cfg.WindowLeft = this.Position.X;
            _cfg.WindowTop = this.Position.Y;
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, "Optimized", _cfg.LastBridge, _cfg.LastCount);
        }

        _statsTimer?.Stop();
        _sessionClockTimer?.Stop();
        _logTimer?.Stop();
        _logClearTimer?.Stop();
        _bootstrapTimer?.Stop();
        _pingTimer?.Stop();
        _saveDebounceTimer?.Stop();
        _toastTimer?.Stop();
        _xrayBootTimer?.Stop();
        _xrayRestartTimer?.Stop();
        if (_geoCts != null) { try { _geoCts.Cancel(); _geoCts.Dispose(); } catch { } }
        StopAllEngines(isClosing: true);
        DisposeTrayIcon();
    }

    private void SidebarBorder_PointerEntered(object? sender, PointerEventArgs e)
    {
        var panLeftStats = this.FindControl<Grid>("panLeftStats");
        if (panLeftStats != null)
        {
            panLeftStats.Opacity = 0.05; // Fade out almost entirely
            panLeftStats.IsHitTestVisible = false;
        }
    }

    private void SidebarBorder_PointerExited(object? sender, PointerEventArgs e)
    {
        var panLeftStats = this.FindControl<Grid>("panLeftStats");
        if (panLeftStats != null)
        {
            panLeftStats.Opacity = 1.0; // Fade back in
            panLeftStats.IsHitTestVisible = true;
        }
    }
}









