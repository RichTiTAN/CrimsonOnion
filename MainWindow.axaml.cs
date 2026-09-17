/*
 * CrimsonOnion - A GUI client that runs multiple Tor instances and load-balances them.
 * Copyright (C) 2026 RichTiTAN
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CrimsonOnion.Models;
using CrimsonOnion.Services;
using CrimsonOnion.Localization;

namespace CrimsonOnion;

public class CountryItem : System.ComponentModel.INotifyPropertyChanged
{
    public string OriginalName { get; set; } = string.Empty;
    public string Name 
    { 
        get => AppStrings.IsPersian ? GeoTranslation.GetCountryFa(Tag, OriginalName) : OriginalName; 
        set 
        { 
            OriginalName = value; 
            OnPropertyChanged(nameof(Name)); 
        }
    }
    public string Tag { get; set; } = string.Empty;
    public Bitmap? Flag { get; set; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void UpdateLanguage() => OnPropertyChanged(nameof(Name));
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}

public partial class MainWindow : Window
{
    private AppConfig _cfg;
    private AppState _state;
    private readonly CrimsonOnion.Services.VpnRuntimeState _vpn = new();
    private readonly CrimsonOnion.Services.TrafficStatsService _traffic;
    private readonly TorLauncherService _tor;
    private readonly BootstrapOrchestrator _bootstrap = new();
    private readonly TorFarmService _torFarm;
    private readonly XraySupervisor _xray;
    private readonly MainWindowLocalizer _strings;
    private readonly Views.OverlayNavigation _overlays;
    private readonly Views.ThemeController _themes;
    private readonly Views.UpdateController _updates;
    private readonly Views.ConnectFlow _connect;
    private readonly Views.BridgePresets _bridges;
    private readonly CrimsonOnion.Services.MoatBridgeClient _moat = new();

    internal AppConfig Cfg => _cfg;
    internal AppState State => _state;
    internal bool IsInitializingSettings => _isInitializingSettings;
    internal string ActiveBridge => _bridges.Active;
    internal string PollMode { get => _pollMode; set => _pollMode = value; }

    internal void TriggerRequestConfigSave() => RequestConfigSave();
    internal void TriggerSmartRestartXray() => SmartRestartXray();
    internal void TriggerApplyModeUI(string mode) => _themes.ApplyModeUI(mode);
    internal void TriggerUpdateModeUI() => _bridges.UpdateModeUI();

    internal void TriggerThemeSelect_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _themes.SelectTheme(sender as global::Avalonia.Controls.Button);

    internal void ApplyGlowSettings() => _themes.ApplyGlowVisibility();

    internal void TriggerCloseAllOverlays() => _overlays.CloseAll();
    internal void TriggerShowToast(string msg, bool success = false) => ShowToast(msg, success);
    internal void TriggerBtnLanguage_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnLanguage_Click(sender, e);
    internal void TriggerBtnLbPolicy_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnLbPolicy_Click(sender, e);
    internal void TriggerSettingsLightDismiss_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e) => SettingsLightDismiss_PointerPressed(sender, e);

    internal void TriggerSidebarHover(bool entered)
    {
        var panLeftStats = this.FindControl<Grid>("panLeftStats");
        if (panLeftStats == null) return;

        panLeftStats.Opacity = entered ? 0.05 : 1.0;
        panLeftStats.IsHitTestVisible = !entered;
    }

    internal void TriggerBtnCheckUpdate_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnCheckUpdate_Click(sender, e);
    internal void TriggerBtnGithub_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnGithub_Click(sender, e);
    internal void TriggerBtnTelegram_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnTelegram_Click(sender, e);
    internal void TriggerBtnOtherApps_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnOtherApps_Click(sender, e);

    private string _pollMode = XraySupervisor.ProxyMode;

    private int _activeTorEngines = 6;

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
    internal void ConnectDisconnect() => _connect.ConnectClicked();
    internal string GetSpeedText()
    {
        var down = this.FindControl<global::Avalonia.Controls.TextBlock>("lblDownloadSpeed")?.Text ?? "0 KB/s";
        var up = this.FindControl<global::Avalonia.Controls.TextBlock>("lblUploadSpeed")?.Text ?? "0 KB/s";
        var total = this.FindControl<global::Avalonia.Controls.TextBlock>("lblTotalData")?.Text ?? "0 MB";
        return $"⬇ {down} | ⬆ {up}\nTotal: {total}";
    }

    protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.Name == "IsActive")
        {
            if (change.NewValue is bool isActive)
            {
                if (isActive) _overlays.RestorePopups();
                else _overlays.RememberPopups();
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        _overlays = new Views.OverlayNavigation(FindNamedControl, TryAutoDisableSplitTunnel);

        CrimsonOnion.Services.AppMessenger.ToastRequested += OnToastRequested;

        DataContext = this;

        _cfg   = new AppConfig();
        _state = new AppState();

        _themes = new Views.ThemeController(
            FindNamedControl,
            () => ActiveBridge,
            () => IsActive,
            RequestConfigSave,
            () => ucSplitTunnel?.UpdateSplitTunnelUI(),
            Resources,
            _cfg,
            _state);

        _updates = new Views.UpdateController(
            FindNamedControl,
            _cfg,
            _vpn,
            ShowToast,
            QuitForInstall,
            OpenInBrowser,
            ShowUpdateDialogAsync,
            ShowPromoDialogAsync);

        _traffic = new CrimsonOnion.Services.TrafficStatsService(_vpn, _state);
        _traffic.Sampled += OnTrafficSampled;
        _tor = new TorLauncherService();
        _tor.ProgressUpdated   += OnTorProgressUpdated;
        _tor.ConnectionDropped += OnTorConnectionDropped;

        _graphScroll = new GraphScroller(() => IsActive);

        _cfg.BaseDir = AppContext.BaseDirectory.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);
        _cfg.CfgFile = System.IO.Path.Combine(_cfg.BaseDir, @"Data\multiplexer_settings.json");
        _cfg.XrayDir = System.IO.Path.Combine(_cfg.BaseDir, @"Data\Xray");
        _cfg.SbDir   = System.IO.Path.Combine(_cfg.BaseDir, @"Data\sing_box");

        ConfigService.Load(_cfg, _state, _cfg.CfgFile);

        _bridges = new Views.BridgePresets(
            _cfg,
            _state,
            _moat,
            FindNamedControl,
            msg => ShowToast(msg),
            RequestConfigSave,
            ApplyLoadedSettings,
            () => _pollMode,
            mode => _pollMode = mode,
            _themes.ApplyModeUI,
            _themes.VpnModeUnavailable);

        _torFarm = new TorFarmService(
            _state,
            _vpn,
            _cfg,
            () => _activeTorEngines,
            (exe, args, workDir) =>
            {
                using var proc = ProcessService.StartProcessDirect(exe, args, workDir);
                return proc?.Id;
            },
            (exe, args, workDir, label) => StartDebugProcess(exe, args, workDir, label),
            _tor.Launch,
            () => _bootstrap.IsProgressPolling,
            StartDnsttTunnels);
        _torFarm.SlotChanged += OnTorSlotChanged;

        _xray = new XraySupervisor(
            _vpn,
            _cfg,
            (exe, args, workDir) =>
            {
                using var proc = ProcessService.StartProcessDirect(exe, args, workDir);
                return proc?.Id;
            },
            (exe, args, workDir, label) => StartDebugProcess(exe, args, workDir, label),
            VpnEngineService.KillPid);

        _connect = new Views.ConnectFlow(
            _cfg,
            _state,
            _vpn,
            _bootstrap,
            _torFarm,
            _xray,
            _tor,
            _traffic,
            _graphScroll,
            FindNamedControl,
            () => ActiveBridge,
            () => _pollMode,
            () => _activeTorEngines,
            msg => ShowToast(msg),
            _themes.UpdateRingAnimation,
            RequestConfigSave,
            UpdateLanIpAsync,
            ApplySystemDnsAsync,
            RestoreSystemDnsAsync,
            () => { UpdateLocalPortUI(); UpdateLanPortUI(); },
            () => { StartSessionClock(); StartGeoPing(); StartStatsPolling(); },
            StartGeoPing,
            StartLogsTimers,
            StopLogsTimers,
            () => _logTailer.Reset());

        ucSidebar?.Initialize(this);

        ucSplitTunnel = this.FindControl<CrimsonOnion.Views.Overlays.SplitTunnelOverlay>("ucSplitTunnel");
        ucSplitTunnel?.Initialize(this);
        ucThemes?.Initialize(this);
        _themes.StartGlow();
        ucSettings?.Initialize(this);
        ucAbout = this.FindControl<CrimsonOnion.Views.Overlays.AboutOverlay>("ucAbout");
        ucAbout?.Initialize(this);
        ucExpert = this.FindControl<CrimsonOnion.Views.Overlays.ExpertOverlay>("ucExpert");
        ucExpert?.Initialize(this);

        _strings = new MainWindowLocalizer(FindNamedControl);

        CrimsonOnion.Services.SimpleLogger.EnableLogging = _cfg.DebugMode;
        CrimsonOnion.Services.SimpleLogger.Log($"[Startup] CrimsonOnion v{Services.UpdateService.AppVersion} — Bridge={_cfg.LastBridge}, Config={_cfg.LastConfig}, Mode={_cfg.LastXrayMode}");
        _themes.ApplyTheme(_cfg.ThemeColor);

        if (int.TryParse(_cfg.LastCount, out int cnt)) _activeTorEngines = cnt;
        _pollMode = _cfg.LastXrayMode ?? XraySupervisor.ProxyMode;

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
            if (isFirstOpen && _cfg.EnableAdapterBinding)
            {
                ucSettings?.TriggerScanAdapters();
            }
            isFirstOpen = false;
        };

        if (_cfg.AutoStart && !_state.IsFirstLaunch)
        {
            _bootstrap.AutoConnect(() =>
            {
                if (!_state.AbortBoot)
                    btnConnect_Click(null, new global::Avalonia.Interactivity.RoutedEventArgs());
            });
        }

        _ = _updates.CheckSilentlyAsync();
    }
    private void TryAutoDisableSplitTunnel()
    {
        if (_cfg.SplitTunnelMode == "DISABLED") return;

        bool hasSavedInput = SplitTunnelService.HasAnySplitInput(_cfg);

        bool hasUnsavedInput = ucSplitTunnel?.HasUnsavedInput ?? false;

        if (hasSavedInput || hasUnsavedInput) return;

        _cfg.SplitTunnelMode = "DISABLED";
        _cfg.EnableDirect = false;
        ucSplitTunnel?.UpdateSplitTunnelUI();
        RequestConfigSave();
    }
    private void LightDismissOverlay_PointerPressed(object? sender, PointerPressedEventArgs e) => _overlays.CloseAll();

    private void CloseOverlay_Click(object? sender, RoutedEventArgs e) => _overlays.CloseAll();

    private void BtnGithub_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/RichTiTAN") { UseShellExecute = true })?.Dispose();
    }

    private void BtnOtherApps_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/RichTiTAN/CrimsonX") { UseShellExecute = true })?.Dispose();
    }

    private void BtnTelegram_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://t.me/itsTitanVPN") { UseShellExecute = true })?.Dispose();
    }


    private async void BtnTitleUpdate_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await _updates.TitleUpdateClicked();

    private async void BtnCheckUpdate_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await _updates.CheckUpdateClicked();

    private async void BtnCrimsonXPromo_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await _updates.PromoClicked();
    private async Task<string?> ShowUpdateDialogAsync(bool isManual, string remoteVer)
        => await new CrimsonOnion.Dialogs.UpdateDialog(isManual, remoteVer).ShowDialog<string>(this);
    private async Task<string?> ShowPromoDialogAsync()
        => await new CrimsonOnion.Dialogs.CrimsonXDialog().ShowDialog<string>(this);
    private static void OpenInBrowser(string url)
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
    private void QuitForInstall()
    {
        ProxyService.SetSystemProxy(false);
        StopAllEngines(true);
        System.Environment.Exit(0);
    }

    private void BtnLanguage_Click(object? sender, RoutedEventArgs e) => _ = _overlays.TogglePopupAsync(Views.OverlayNavigation.LanguagePopup);

    private void LanguageOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string lang)
        {
            var lbl = ucSettings?.FindControl<TextBlock>("lblCurrentLanguage");
            if (lbl != null) lbl.Text = lang;

            _cfg.Language = lang;
            SaveConfig();
            ApplyLanguage();

            _ = _overlays.ClosePopupsAsync();
        }
    }

    private void BtnLbPolicy_Click(object? sender, RoutedEventArgs e) => _ = _overlays.TogglePopupAsync(Views.OverlayNavigation.LbPolicyPopup);

    private void LbPolicyOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string policy)
        {
            string displayName = policy switch
            {
                "leastload"  => "LEAST LOAD",
                "roundrobin" => "ROUND ROBIN",
                "leastping"  => "LEAST PING",
                "random"     => "RANDOM",
                _            => policy.ToUpperInvariant()
            };

            var lbl = ucSettings?.FindControl<TextBlock>("lblCurrentLbPolicy");
            if (lbl != null) lbl.Text = displayName;

            bool wasConnected = _state.IsConnected || _state.IsEngineRunning;
            _cfg.XrayBalancePolicy = policy;
            SaveConfig();

            if (wasConnected)
                SmartRestartXray();

            _ = _overlays.ClosePopupsAsync();
        }
    }

    private void SettingsLightDismiss_PointerPressed(object? sender, PointerPressedEventArgs e) => _overlays.CloseSettingsPopups();

    private Control? FindNamedControl(string name) =>
        this.FindControl<Control>(name)
        ?? ucSidebar?.FindControl<Control>(name)
        ?? ucSettings?.FindControl<Control>(name)
        ?? ucSplitTunnel?.FindControl<Control>(name)
        ?? ucThemes?.FindControl<Control>(name)
        ?? ucAbout?.FindControl<Control>(name)
        ?? ucExpert?.FindControl<Control>(name);

    private TextBlock? F(string name) => FindNamedControl(name) as TextBlock;
    private Button? B(string name)    => FindNamedControl(name) as Button;
    private TextBox? T(string name)   => FindNamedControl(name) as TextBox;

    private void ApplyLanguage()
    {
        _strings.ApplyLanguage(_cfg.Language, _state.IsConnected, _state.IsEngineRunning);

        foreach (var c in Countries) c.UpdateLanguage();
        ApplyRoutingUI(false);

        if (_trayWidget != null)
            _trayWidget.ApplyLanguage(AppStrings.IsPersian);

        UpdateLanPortUI();
        UpdateLocalPortUI();

        RelocalizeGeo();

        ucSettings?.TriggerUpdateAdapterBindingMutualExclusivity();
        _themes.ApplyModeUI(_cfg.LastXrayMode);

        ucSplitTunnel?.UpdateSplitTunnelUI();
    }

    private void MainCountries_Click(object? sender, RoutedEventArgs e) => _ = _overlays.TogglePopupAsync(Views.OverlayNavigation.MainCountriesPopup);

    private void Mode_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button clickedBtn)
        {
            if (clickedBtn.Name == Views.ThemeController.VpnModeButton && _themes.VpnModeUnavailable()) return;

            string newMode = Views.ThemeController.ModeOf(clickedBtn.Name);

            bool modeChanged = _cfg.LastXrayMode != newMode;
            if (modeChanged) _cfg.LastXrayMode = newMode;

            _themes.ApplyModeUI(newMode);

            if (modeChanged)
            {
                _pollMode         = newMode;
                RequestConfigSave();
                SmartRestartXray();
            }
        }
    }

    private void Bridge_Click(object? sender, RoutedEventArgs e) => _bridges.SelectBridge(sender as Button);

    private void AdvancedBridgeMode_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;

        _bridges.ToggleAdvancedMode(sender as Button);
    }

    private void btnCustomSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _bridges.SaveCustom();
    private void btnCustomCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _bridges.CancelCustom();
    private void btnGetWebTunnel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _bridges.FetchWebTunnel();
    private void btnGetObfs4_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _bridges.FetchObfs4();
    private void btnCaptchaCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _bridges.CancelCaptcha();
    private void btnCaptchaSubmit_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _bridges.SubmitCaptcha();
    private void txtCaptchaSol_KeyDown(object? sender, KeyEventArgs e) => _bridges.CaptchaKeyDown(e);

    private void Engines_ValueChanged(object? sender, global::Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Slider slider)
        {
            int engines = (int)slider.Value;
            var lbl = F("lblEngineCount");
            if (lbl != null) lbl.Text = engines.ToString();

            if (_activeTorEngines == engines) return;

            _activeTorEngines = engines;
            _cfg.LastCount    = engines.ToString();

            if (_state.IsEngineRunning)
                OnEngineCountChanged(engines);
            else
            {
                _torFarm.RefreshSlots();
                RequestConfigSave();
            }
        }
    }

    private void ApplyLoadedSettings()
    {
        _bridges.UpdateBridgeUI();

        var sldEngines = this.FindControl<global::Avalonia.Controls.Slider>("sldEngines");
        if (sldEngines != null) sldEngines.Value = _activeTorEngines;
        var lblEngineCount = F("lblEngineCount");
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

        var togSysDns = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togSysDns");
        if (togSysDns != null) togSysDns.IsChecked = _cfg.EnableSystemDns;

        var txtSysDnsPrimary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
        if (txtSysDnsPrimary != null) txtSysDnsPrimary.Text = _cfg.SystemDnsPrimary;

        var txtSysDnsSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
        if (txtSysDnsSecondary != null) txtSysDnsSecondary.Text = _cfg.SystemDnsSecondary;

        var btnAdBlockTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnAdBlockTog");
        if (btnAdBlockTog != null) btnAdBlockTog.IsChecked = _cfg.EnableAdBlock;

        var btnLanTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnLanTog");
        if (btnLanTog != null) btnLanTog.IsChecked = _cfg.AllowLanConnections;

        var togLanAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togLanAuth");
        if (togLanAuth != null) togLanAuth.IsChecked = _cfg.EnableLanAuth;

        var btnDebugTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnDebugTog");
        if (btnDebugTog != null) btnDebugTog.IsChecked = _cfg.DebugMode;

        var togOutboundProxy = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundProxy");
        if (togOutboundProxy != null) togOutboundProxy.IsChecked = _cfg.EnableOutboundProxy;

        var togXrayExitNode = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togXrayExitNode");
        if (togXrayExitNode != null) togXrayExitNode.IsChecked = _cfg.EnableV2rayChain;

        var togDirectUDP = ucSplitTunnel?.FindControl<global::Avalonia.Controls.ToggleSwitch>("togDirectUDP")
                        ?? this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togDirectUDP");
        if (togDirectUDP != null) togDirectUDP.IsChecked = _cfg.EnableDirectUDP;

        var panOutboundProxy = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
        var icoOutboundExpander = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");
        var cmbOutboundType = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbOutboundType");
        var txtOutboundAddr = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundAddr");
        var txtOutboundPort = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPort");
        var togOutboundAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundAuth");
        var panOutboundAuth = this.FindControl<global::Avalonia.Controls.Border>("panOutboundAuth");
        var txtOutboundUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundUser");
        var txtOutboundPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPass");
        var togAdapterBinding = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togAdapterBinding");
        var panAdapterBinding = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBinding");
        var icoAdapterBindingExpander = this.FindControl<global::Avalonia.Controls.PathIcon>("icoAdapterBindingExpander");

        if (togOutboundProxy != null)
        {
            togOutboundProxy.IsChecked = _cfg.EnableOutboundProxy;
            if (_cfg.EnableOutboundProxy && panOutboundProxy != null && icoOutboundExpander != null)
            {
            }
            if (cmbOutboundType != null) cmbOutboundType.SelectedIndex = _cfg.OutboundProxyType == "HTTPS" ? 1 : 0;
            if (txtOutboundAddr != null) txtOutboundAddr.Text = _cfg.OutboundProxyAddress;
            if (txtOutboundPort != null) txtOutboundPort.Text = _cfg.OutboundProxyPort;
            if (togOutboundAuth != null)
            {
                togOutboundAuth.IsChecked = _cfg.EnableOutboundAuth;
                if (_cfg.EnableOutboundAuth && panOutboundAuth != null)
                {
                }
            }
            if (txtOutboundUser != null) txtOutboundUser.Text = _cfg.OutboundProxyUser;
            if (txtOutboundPass != null) txtOutboundPass.Text = _cfg.OutboundProxyPass;
        }

        if (togAdapterBinding != null)
        {
            togAdapterBinding.IsChecked = _cfg.EnableAdapterBinding;
            ucSettings?.TriggerUpdateAdapterBindingMutualExclusivity();
        }

        _isInitializingSettings = false;
        ApplyGlowSettings();

        _themes.ApplyModeUI(_pollMode);
        ApplyRoutingUI(false);
        UpdateLanPortUI();
        _bridges.UpdateAdvancedUI();

        var langLbl = F("lblCurrentLanguage");
        if (langLbl != null) langLbl.Text = _cfg.Language;
        ApplyLanguage();

        var lbLbl = F("lblCurrentLbPolicy");
        if (lbLbl != null)
        {
            lbLbl.Text = _cfg.XrayBalancePolicy switch
            {
                "leastload"  => "LEAST LOAD",
                "leastping"  => "LEAST PING",
                "roundrobin" => "ROUND ROBIN",
                "random"     => "RANDOM",
                _            => (_cfg.XrayBalancePolicy ?? "leastping").ToUpperInvariant()
            };
        }
    }

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
            _cfg.WindowTop  = this.Position.Y;
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
        }

        CrimsonOnion.Services.AppMessenger.ToastRequested -= OnToastRequested;

        // ── Stop & null all timers ──────────────────────────────────────────
        _themes.StopGlow();
        _bootstrap.StopAll();
        _saveDebounceTimer?.Stop(); _saveDebounceTimer = null;
        _toastTimer?.Stop();        _toastTimer       = null;
        _connect.StopFillAnimation();
        _graphScroll.Stop();
        _logTimer?.Stop();          _logTimer         = null;
        _logClearTimer?.Stop();     _logClearTimer    = null;

        // ── Cancel & dispose all CancellationTokenSources ──────────────────
        _traffic.Sampled -= OnTrafficSampled;
        _torFarm.SlotChanged -= OnTorSlotChanged;
        _traffic.Stop();
        CancellationTokens.CancelAndDispose(ref _vpn.PingCts);
        CancellationTokens.CancelAndDispose(ref _vpn.GeoCts);

        // ── Misc resources ──────────────────────────────────────────────────
        _moat.Dispose();

        StopAllEngines(isClosing: true);
        foreach (var country in Countries)
            country.Flag?.Dispose();
        DisposeTrayIcon();
    }

}

