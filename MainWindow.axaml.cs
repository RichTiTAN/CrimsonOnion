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
    private List<TorControlClient> _torControlClients = new();
    
    internal AppConfig Cfg => _cfg;
    internal AppState State => _state;
    internal bool IsInitializingSettings => _isInitializingSettings;
    internal string ActiveBridge => _activeBridge;
    internal string PollMode { get => _pollMode; set => _pollMode = value; }
    
    internal void TriggerRequestConfigSave() => RequestConfigSave();
    internal void TriggerSmartRestartXray() => SmartRestartXray();
    internal void TriggerApplyModeUI(string mode) => ApplyModeUI(mode);
    internal void TriggerUpdateSplitTunnelUI() => ucSplitTunnel?.UpdateSplitTunnelUI();
    
    internal void TriggerThemeSelect_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => ThemeSelect_Click(sender, e);
    
    internal void ApplyGlowSettings()
    {
        var pan = this.FindControl<global::Avalonia.Controls.Panel>("panOuterGlow");
        if (pan != null)
        {
            pan.IsVisible = !_cfg.DisableGlow;
        }

        var bgEllipse = this.FindControl<global::Avalonia.Controls.Shapes.Ellipse>("bgGlowEllipse");
        if (bgEllipse != null)
        {
            bgEllipse.IsVisible = !_cfg.DisableGlow;
        }
    }

    internal void TriggerCloseAllOverlays() => CloseAllOverlays();
    internal void TriggerShowToast(string msg, bool success = false) => ShowToast(msg, success);
    internal void TriggerApplyLoadedSettings() => ApplyLoadedSettings();
    internal void TriggerApplyLanguage() => ApplyLanguage();
    internal void TriggerBtnLanguage_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnLanguage_Click(sender, e);
    internal void TriggerBtnLbPolicy_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => BtnLbPolicy_Click(sender, e);
    internal void TriggerSettingsLightDismiss_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e) => SettingsLightDismiss_PointerPressed(sender, e);



    
    private global::Avalonia.Threading.DispatcherTimer? _glowTimer;
    private double _glowAngle = 0;
    private double _glowTime = 0;

    private void SetupGlowTimer()
    {
        if (_glowTimer == null)
        {
            _glowTimer = new global::Avalonia.Threading.DispatcherTimer();
            _glowTimer.Interval = System.TimeSpan.FromMilliseconds(33); // ~30 fps
            _glowTimer.Tick += GlowTimer_Tick;
            _glowTimer.Start();
        }
    }

    private void GlowTimer_Tick(object? sender, System.EventArgs e)
    {
        if (_cfg.PauseGlow || _cfg.DisableGlow || !this.IsActive) return;

        _glowAngle += 4.0; 
        if (_glowAngle >= 360) _glowAngle -= 360;

        _glowTime += 0.033; 

        var rectOuter = this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("rectOuterGlow");
        if (rectOuter != null && rectOuter.RenderTransform is global::Avalonia.Media.RotateTransform rtOuter)
        {
            rtOuter.Angle = _glowAngle;
        }

        var bgEllipse = this.FindControl<global::Avalonia.Controls.Shapes.Ellipse>("bgGlowEllipse");
        if (bgEllipse != null && bgEllipse.RenderTransform is global::Avalonia.Media.TranslateTransform ttBg)
        {
            ttBg.X = System.Math.Sin(_glowTime * 0.14) * 350;
            ttBg.Y = 150 + System.Math.Cos(_glowTime * 0.21) * 100;
        }
    }

    private int?[] _torPids = new int?[8];
    private int? _xrayDebugPid, _sbDebugPid;
    private int? _xrayPid, _adapterXrayPid, _sbPid;
    private DispatcherTimer? _bootstrapTimer;
    private DispatcherTimer? _autoBootTimer; 
    private int _pollSelCount = 6;
    private string _pollMode = "Proxy Mode";
    private string _pollSelBridge = "Direct";

    private string _activeBridge = "Direct";
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
    internal void ConnectDisconnect() => btnConnect_Click(null, new global::Avalonia.Interactivity.RoutedEventArgs());
    internal string GetSpeedText()
    {
        var down = this.FindControl<global::Avalonia.Controls.TextBlock>("lblDownloadSpeed")?.Text ?? "0 KB/s";
        var up = this.FindControl<global::Avalonia.Controls.TextBlock>("lblUploadSpeed")?.Text ?? "0 KB/s";
        var total = this.FindControl<global::Avalonia.Controls.TextBlock>("lblTotalData")?.Text ?? "0 MB";
        return $"⬇ {down} | ⬆ {up}\nTotal: {total}";
    }

    private bool _wasLanguagePopupOpen = false;
    private bool _wasCountriesPopupOpen = false;
    private bool _wasLbPolicyPopupOpen = false;

    protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.Name == "IsActive")
        {
            if (change.NewValue is bool isActive)
            {
                if (isActive)
                {
                    if (_wasLanguagePopupOpen && LanguagePopup != null)
                    {
                        LanguagePopup.IsOpen = true;
                    }
                    if (_wasCountriesPopupOpen && CountriesPopup != null)
                    {
                        CountriesPopup.IsOpen = true;
                    }
                    if (_wasLbPolicyPopupOpen && LbPolicyPopup != null)
                    {
                        LbPolicyPopup.IsOpen = true;
                    }
                }
                else
                {
                    if (LanguagePopup != null)
                    {
                        _wasLanguagePopupOpen = LanguagePopup.IsOpen;
                        if (LanguagePopup.IsOpen) LanguagePopup.IsOpen = false;
                    }
                    if (CountriesPopup != null)
                    {
                        _wasCountriesPopupOpen = CountriesPopup.IsOpen;
                        if (CountriesPopup.IsOpen) CountriesPopup.IsOpen = false;
                    }
                    if (LbPolicyPopup != null)
                    {
                        _wasLbPolicyPopupOpen = LbPolicyPopup.IsOpen;
                        if (LbPolicyPopup.IsOpen) LbPolicyPopup.IsOpen = false;
                    }
                }
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        CrimsonOnion.Services.AppMessenger.ToastRequested += (msg, success) => ShowToast(msg, success);
        
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
        _cfg.SbDir   = System.IO.Path.Combine(_cfg.BaseDir, @"Data\sing_box");

        ConfigService.Load(_cfg, _state, _cfg.CfgFile);
        ucSplitTunnel = this.FindControl<CrimsonOnion.Views.Overlays.SplitTunnelOverlay>("ucSplitTunnel");
        ucSplitTunnel?.Initialize(this);
        ucThemes?.Initialize(this);
        SetupGlowTimer();
        ucSettings?.Initialize(this);
        CrimsonOnion.Services.SimpleLogger.EnableLogging = _cfg.DebugMode;
        CrimsonOnion.Services.SimpleLogger.Log($"[Startup] CrimsonOnion v{Services.UpdateService.AppVersion} — Bridge={_cfg.LastBridge}, Config={_cfg.LastConfig}, Mode={_cfg.LastXrayMode}");
        ApplyTheme(_cfg.ThemeColor);

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
            if (isFirstOpen && _cfg.EnableAdapterBinding)
            {
                ucSettings?.TriggerScanAdapters();
            }
            isFirstOpen = false;
        };

        if (_cfg.AutoStart && !_state.IsFirstLaunch)
        {
            _autoBootTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _autoBootTimer.Tick += (s, ev) =>
            {
                _autoBootTimer?.Stop();
                if (!_state.AbortBoot)
                    btnConnect_Click(null, new global::Avalonia.Interactivity.RoutedEventArgs());
            };
            _autoBootTimer.Start();
        }


        _ = CheckUpdateSilentAsync();
    }


    private async Task ClosePopupAnimatedAsync()
    {
        bool closeCountries = CountriesPopup != null && CountriesPopup.IsOpen;
        bool closeLanguage = LanguagePopup != null && LanguagePopup.IsOpen;
        bool closeLbPolicy = LbPolicyPopup != null && LbPolicyPopup.IsOpen;

        if (!closeCountries && !closeLanguage && !closeLbPolicy) return;

        if (closeCountries && CountriesPopup?.Child is Border cBorder) cBorder.Classes.Remove("popupOpen");
        if (closeLanguage && LanguagePopup?.Child is Border lBorder) lBorder.Classes.Remove("popupOpen");
        if (closeLbPolicy && LbPolicyPopup?.Child is Border lpBorder) lpBorder.Classes.Remove("popupOpen");

        await Task.Delay(200);

        if (closeCountries && CountriesPopup != null) CountriesPopup.IsOpen = false;
        if (closeLanguage && LanguagePopup != null) LanguagePopup.IsOpen = false;
        if (closeLbPolicy && LbPolicyPopup != null) LbPolicyPopup.IsOpen = false;
        
        bool anyPopupOpen = (CountriesPopup != null && CountriesPopup.IsOpen) || (LanguagePopup != null && LanguagePopup.IsOpen) || (LbPolicyPopup != null && LbPolicyPopup.IsOpen);
        if (!anyPopupOpen)
        {
            var sld = ucSettings?.FindControl<Border>("SettingsLightDismiss");
            if (sld != null) sld.IsVisible = false;
            
            var panSettings = this.FindControl<Border>("panSettingsOverlay");
            var panSplit = ucSplitTunnel;
            var panExpert = this.FindControl<Border>("panExpertOverlay");
            var panAbout = this.FindControl<Border>("panAboutOverlay");
            if ((panSettings == null || !panSettings.IsVisible) &&
                (panSplit == null || !panSplit.IsVisible) &&
                (panExpert == null || !panExpert.IsVisible) &&
                (panAbout == null || !panAbout.IsVisible))
            {
                LightDismissOverlay.IsVisible = false;
            }
        }
    }

    private void CloseAllOverlays()
    {
        var panSplitOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSplitOverlay");
        if (panSplitOverlay != null && panSplitOverlay.IsVisible)
        {
            if (_cfg.SplitTunnelMode != "DISABLED")
            {
                bool hasSavedInput = !string.IsNullOrWhiteSpace(_cfg.LastManualSplit) || 
                                     !string.IsNullOrWhiteSpace(_cfg.LastAppSplit) || 
                                     !string.IsNullOrWhiteSpace(_cfg.LastBlockSplit);
                                     
                bool hasUnsavedInput = ucSplitTunnel?.HasUnsavedInput ?? false;

                if (!hasSavedInput && !hasUnsavedInput)
                {
                    _cfg.SplitTunnelMode = "DISABLED";
                    _cfg.EnableDirect = false;
                    ucSplitTunnel?.UpdateSplitTunnelUI();
                    RequestConfigSave();
                }
            }

            panSplitOverlay.Classes.Remove("popupOpen");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panSplitOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
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
        
        var panThemesOverlay = this.FindControl<Border>("panThemesOverlay");
        if (panThemesOverlay != null && panThemesOverlay.IsVisible)
        {
            panThemesOverlay.Classes.Remove("popupOpen");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panThemesOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }

        var panAboutOverlay = this.FindControl<Border>("panAboutOverlay");
        if (panAboutOverlay != null && panAboutOverlay.IsVisible)
        {
            panAboutOverlay.Classes.Remove("popupOpen");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panAboutOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        
        var ldo = this.FindControl<Border>("LightDismissOverlay");
        if (ldo != null) ldo.IsVisible = false;
        
        if ((CountriesPopup != null && CountriesPopup.IsOpen) || (LanguagePopup != null && LanguagePopup.IsOpen) || (LbPolicyPopup != null && LbPolicyPopup.IsOpen))
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
        var panAboutOverlay = this.FindControl<Border>("panAboutOverlay");
        if (panAboutOverlay != null && !panAboutOverlay.IsVisible)
        {
            CloseAllOverlays();
            panAboutOverlay.IsVisible = true;
            panAboutOverlay.Classes.Add("popupOpen");
            var ldo = this.FindControl<Border>("LightDismissOverlay");
            if (ldo != null) ldo.IsVisible = true;
        }
    }

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

    private async void BtnCopyAddress_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string address)
        {
            var clipboard = global::Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(address);
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastAddressCopied, success: true);
            }
        }
    }

    private CancellationTokenSource? _updateCts;
    private string _remoteUpdateVersion = "0.0.0";
    private string _remoteMinUpdateVersion = "0.0.0";

        private void SetUpdateUIStatus(string status)
    {
        var btnTitleUpdate = this.FindControl<global::Avalonia.Controls.Button>("btnTitleUpdate");
        if (btnTitleUpdate != null) btnTitleUpdate.Content = status;
        
        var btnCheckUpdate = this.FindControl<global::Avalonia.Controls.Button>("btnCheckUpdate");
        if (btnCheckUpdate != null) btnCheckUpdate.Content = status;
    }

    private async Task CheckUpdateSilentAsync()
    {
        try
        {
            var (remoteVer, remoteMin) = await Services.UpdateService.CheckForUpdatesAsync();
            if (remoteVer != null)
            {
                _remoteUpdateVersion = remoteVer;
                _remoteMinUpdateVersion = remoteMin ?? "0.0.0";
                var btnTitleUpdate = this.FindControl<global::Avalonia.Controls.Button>("btnTitleUpdate");
                if (btnTitleUpdate != null) btnTitleUpdate.IsVisible = true;
                
                string msg = CrimsonOnion.Localization.AppStrings.UpdateAutoTitle;
                SetUpdateUIStatus(msg);
            }
        }
        catch (Exception ex)
        {
            CrimsonOnion.Services.SimpleLogger.Log(ex);
        }
    }

    private async Task StartUpdateDownloadAsync()
    {
        if (_updateCts != null)
        {
            _updateCts.Cancel();
            _updateCts.Dispose();
            _updateCts = null;
            return;
        }

        if (string.IsNullOrEmpty(_remoteUpdateVersion) || _remoteUpdateVersion == "0.0.0") return;

        _updateCts = new System.Threading.CancellationTokenSource();
        var token = _updateCts.Token;

        try
        {
            await Services.UpdateService.DownloadAndInstallUpdateAsync(_remoteUpdateVersion, _cfg.BaseDir, (status) => 
            {
                SetUpdateUIStatus(status);
            }, token);

            ProxyService.SetSystemProxy(false);
            StopAllEngines(true);
            System.Environment.Exit(0);
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.UpdateCancelled);
            }
            else
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastUpdateDownloadFailed);
            }
            string msg = CrimsonOnion.Localization.AppStrings.UpdateAutoTitle;
            SetUpdateUIStatus(msg);
        }
        catch (Exception ex)
        {
            ShowToast(string.Format(CrimsonOnion.Localization.AppStrings.ToastUpdateErrorFormat, ex.Message));
            string msg = CrimsonOnion.Localization.AppStrings.UpdateAutoTitle;
            SetUpdateUIStatus(msg);
        }
        finally
        {
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    private async void BtnCrimsonXPromo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new Dialogs.CrimsonXDialog();
        var result = await dialog.ShowDialog<string>(this);
        
        if (result == "Primary")
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/RichTiTAN/CrimsonX") { UseShellExecute = true })?.Dispose();
        }
    }

    private async void BtnTitleUpdate_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_updateCts != null)
        {
            _updateCts.Cancel();
            _updateCts.Dispose();
            _updateCts = null;
            return;
        }

        bool isManual = Version.Parse(Services.UpdateService.AppVersion) < Version.Parse(_remoteMinUpdateVersion);
        var dialog = new Dialogs.UpdateDialog(isManual: isManual, _remoteUpdateVersion);
        var result = await dialog.ShowDialog<string>(this);
        
        if (result == "Primary")
        {
            if (isManual)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true });
            else
                _ = StartUpdateDownloadAsync();
        }
        else if (result == "Secondary")
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true });
        }
    }

    private async void BtnCheckUpdate_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var btnCheckUpdate = this.FindControl<global::Avalonia.Controls.Button>("btnCheckUpdate");
        if (btnCheckUpdate == null) return;

        if (_updateCts != null)
        {
            _updateCts.Cancel();
            _updateCts.Dispose();
            _updateCts = null;
            return;
        }

        if (!string.IsNullOrEmpty(_remoteUpdateVersion) && _remoteUpdateVersion != "0.0.0")
        {
            bool isManual = Version.Parse(Services.UpdateService.AppVersion) < Version.Parse(_remoteMinUpdateVersion);
            var dialog = new Dialogs.UpdateDialog(isManual: isManual, _remoteUpdateVersion);
            var result = await dialog.ShowDialog<string>(this);
            
            if (result == "Primary")
            {
                if (isManual)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true });
                else
                    _ = StartUpdateDownloadAsync();
            }
            else if (result == "Secondary")
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true });
            }
            return;
        }

        if (btnCheckUpdate != null) btnCheckUpdate.Content = CrimsonOnion.Localization.AppStrings.UpdateChecking;
        _updateCts = new System.Threading.CancellationTokenSource();
        var token = _updateCts.Token;

        try
        {
            var (remoteVer, remoteMin) = await Services.UpdateService.CheckForUpdatesAsync(token);
            if (remoteVer == null)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastLatestVersion, success: true);
                if (btnCheckUpdate != null) btnCheckUpdate.Content = CrimsonOnion.Localization.AppStrings.UpdateLatest;
                try { await Task.Delay(3000, token); } catch { }
                if (btnCheckUpdate != null) btnCheckUpdate.Content = CrimsonOnion.Localization.AppStrings.CheckForUpdates;
                _updateCts?.Dispose();
                _updateCts = null;
                return;
            }

            if (Version.Parse(Services.UpdateService.AppVersion) < Version.Parse(remoteMin ?? "0.0.0"))
            {
                if (btnCheckUpdate != null) btnCheckUpdate.Content = CrimsonOnion.Localization.AppStrings.UpdateManual;
                
                var dialog = new Dialogs.UpdateDialog(isManual: true, remoteVer);
                var result = await dialog.ShowDialog<string>(this);
                
                if (result == "Primary" || result == "Secondary")
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true });
                }
                
                _updateCts?.Dispose();
                _updateCts = null;
                return;
            }

            _remoteUpdateVersion = remoteVer;
            var btnTitleUpdate = this.FindControl<global::Avalonia.Controls.Button>("btnTitleUpdate");
            if (btnTitleUpdate != null) btnTitleUpdate.IsVisible = true;

            string msg = CrimsonOnion.Localization.AppStrings.UpdateAutoTitle;
            SetUpdateUIStatus(msg);

            _updateCts?.Dispose();
            _updateCts = null;

            var dialog2 = new Dialogs.UpdateDialog(isManual: false, remoteVer);
            var result2 = await dialog2.ShowDialog<string>(this);
            
            if (result2 == "Primary")
            {
                _ = StartUpdateDownloadAsync();
            }
            else if (result2 == "Secondary")
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/RichTiTAN/CrimsonOnion/releases") { UseShellExecute = true });
            }
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.UpdateCancelled);
                if (btnCheckUpdate != null) btnCheckUpdate.Content = CrimsonOnion.Localization.AppStrings.UpdateCancelled;
                try { await Task.Delay(2000); } catch { }
            }
            else
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastUpdateCheckFailed);
            }
            if (btnCheckUpdate != null) btnCheckUpdate.Content = CrimsonOnion.Localization.AppStrings.CheckForUpdates;
        }
        catch (Exception ex)
        {
            ShowToast(string.Format(CrimsonOnion.Localization.AppStrings.ToastUpdateErrorFormat, ex.Message));
            if (btnCheckUpdate != null) btnCheckUpdate.Content = CrimsonOnion.Localization.AppStrings.CheckForUpdates;
        }
        finally
        {
            if (_updateCts != null)
            {
                _updateCts?.Dispose();
                _updateCts = null;
            }
        }
    }

private async void BtnLanguage_Click(object? sender, RoutedEventArgs e)
    {
        bool isSelf = LanguagePopup != null && LanguagePopup.IsOpen && LanguagePopup.PlacementTarget?.Name == "btnLanguage";
        if (isSelf)
        {
            _ = ClosePopupAnimatedAsync();
            return;
        }

        if (LanguagePopup != null && LanguagePopup.IsOpen)
        {
            LanguagePopup.IsOpen = false;
            if (LanguagePopup.Child is Border oldBorder) oldBorder.Classes.Remove("popupOpen");
        }

        _ = ClosePopupAnimatedAsync();

        if (LanguagePopup != null)
        {
            LanguagePopup.PlacementTarget  = ucSettings?.FindControl<Control>("btnLanguage");
            LanguagePopup.Placement        = PlacementMode.Bottom;
            LanguagePopup.HorizontalOffset = 0;
            LanguagePopup.VerticalOffset   = 5;
            LanguagePopup.IsOpen           = true;
            
            var sld = ucSettings?.FindControl<Border>("SettingsLightDismiss");
            if (sld != null) sld.IsVisible = true;
            
            await Task.Delay(10);
            if (LanguagePopup.Child is Border border) border.Classes.Add("popupOpen");
        }
    }

    private void LanguageOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string lang)
        {
            var lbl = ucSettings?.FindControl<TextBlock>("lblCurrentLanguage");
            if (lbl != null) lbl.Text = lang;

            _cfg.Language = lang;
            SaveConfig();
            ApplyLanguage();

            _ = ClosePopupAnimatedAsync();
        }
    }

    private async void BtnLbPolicy_Click(object? sender, RoutedEventArgs e)
    {
        bool isSelf = LbPolicyPopup != null && LbPolicyPopup.IsOpen && LbPolicyPopup.PlacementTarget?.Name == "btnLbPolicy";
        if (isSelf)
        {
            _ = ClosePopupAnimatedAsync();
            return;
        }

        if (LbPolicyPopup != null && LbPolicyPopup.IsOpen)
        {
            LbPolicyPopup.IsOpen = false;
            if (LbPolicyPopup.Child is Border oldBorder) oldBorder.Classes.Remove("popupOpen");
        }

        _ = ClosePopupAnimatedAsync();

        if (LbPolicyPopup != null)
        {
            LbPolicyPopup.PlacementTarget  = ucSettings?.FindControl<Control>("btnLbPolicy");
            LbPolicyPopup.Placement        = PlacementMode.Bottom;
            LbPolicyPopup.HorizontalOffset = 0;
            LbPolicyPopup.VerticalOffset   = 5;
            LbPolicyPopup.IsOpen           = true;

            var sld = ucSettings?.FindControl<Border>("SettingsLightDismiss");
            if (sld != null) sld.IsVisible = true;

            await Task.Delay(10);
            if (LbPolicyPopup.Child is Border border) border.Classes.Add("popupOpen");
        }
    }

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
            _cfg.HaProxyBalancePolicy = policy;
            SaveConfig();

            if (wasConnected)
                SmartRestartXray();

            _ = ClosePopupAnimatedAsync();
        }
    }

    private void SettingsLightDismiss_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((LanguagePopup != null && LanguagePopup.IsOpen) || (LbPolicyPopup != null && LbPolicyPopup.IsOpen))
        {
            _ = ClosePopupAnimatedAsync();
        }
    }


    
    private TextBlock? F(string name) => this.FindControl<TextBlock>(name) ?? ucSettings?.FindControl<TextBlock>(name) ?? ucSplitTunnel?.FindControl<TextBlock>(name) ?? ucThemes?.FindControl<TextBlock>(name);
    private Button? B(string name)    => this.FindControl<Button>(name) ?? ucSettings?.FindControl<Button>(name) ?? ucSplitTunnel?.FindControl<Button>(name) ?? ucThemes?.FindControl<Button>(name);
    private TextBox? T(string name)   => this.FindControl<TextBox>(name) ?? ucSettings?.FindControl<TextBox>(name) ?? ucSplitTunnel?.FindControl<TextBox>(name) ?? ucThemes?.FindControl<TextBox>(name);

    private void ApplyLanguage()
    {
        AppStrings.SetLanguage(_cfg.Language);
        bool fa = AppStrings.IsPersian;



        AppStrings.Apply(F("lblSidebarConnection"),  AppStrings.Connect);
        AppStrings.Apply(F("lblSidebarCountries"),   AppStrings.SidebarCountries);
        AppStrings.Apply(F("lblSidebarSplitTunnel"), AppStrings.SidebarSplitTunnel);
        AppStrings.Apply(F("lblSidebarSettings"),    AppStrings.SidebarSettings);
        AppStrings.Apply(F("lblSidebarAbout"),       AppStrings.SidebarAbout);

        AppStrings.Apply(F("lblThemesHeader"),       AppStrings.SidebarThemes, forceLtr: true);
        AppStrings.Apply(F("lblPauseGlow"),          AppStrings.ThemesPauseGlow);
        AppStrings.Apply(F("lblPauseGlowDesc"),      AppStrings.ThemesPauseGlowDesc);
        AppStrings.Apply(F("lblDisableGlow"),        AppStrings.ThemesDisableGlow);
        AppStrings.Apply(F("lblDisableGlowDesc"),    AppStrings.ThemesDisableGlowDesc);

        AppStrings.Apply(F("lblProxyMode"),  AppStrings.ProxyMode);
        AppStrings.Apply(F("lblVpnMode"),    AppStrings.VpnMode);
        AppStrings.Apply(F("lblClearProxy"), AppStrings.ClearProxy);

        AppStrings.ApplyToolTip(B("btnProxyMode"),  AppStrings.TtProxyMode);
        AppStrings.ApplyToolTip(B("btnVpnMode"),    AppStrings.TtVpnMode);
        AppStrings.ApplyToolTip(B("btnClearProxy"), AppStrings.TtClearProxy);
        
        AppStrings.ApplyToolTip(B("btnLbLeastLoad"), AppStrings.TtLbLeastLoad);
        AppStrings.ApplyToolTip(B("btnLbRoundRobin"), AppStrings.TtLbRoundRobin);
        AppStrings.ApplyToolTip(B("btnLbLeastPing"), AppStrings.TtLbLeastPing);
        AppStrings.ApplyToolTip(B("btnLbRandom"), AppStrings.TtLbRandom);

        
        var panTimerContent = this.FindControl<StackPanel>("panTimerContent");
        if (panTimerContent != null)
        {
            panTimerContent.FlowDirection = fa 
                ? global::Avalonia.Media.FlowDirection.RightToLeft 
                : global::Avalonia.Media.FlowDirection.LeftToRight;
        }

        AppStrings.Apply(F("lblBridgeType"),    AppStrings.BridgeType);
        AppStrings.Apply(F("lblGetBridges"),    AppStrings.GetBridges);
        AppStrings.Apply(F("lblTorEngines"),    AppStrings.TorEngines);
        AppStrings.Apply(F("lblLogsStatus"),    AppStrings.LogsStatus);
        AppStrings.Apply(F("lblTorBootstrap"),  AppStrings.TorBootstrap);
        AppStrings.Apply(F("lblXrayLogHeader"), AppStrings.XrayLogHeader);
        AppStrings.Apply(F("lblConnectedFor"),  AppStrings.ConnectedFor);
        AppStrings.Apply(F("lblConnectedTo"),   AppStrings.ConnectedTo);
        AppStrings.Apply(F("lblDisconnected"),  AppStrings.Disconnected);
        AppStrings.Apply(F("lblLocalPortLabel"), AppStrings.OpenLocalPort);
        AppStrings.Apply(F("lblLanPortLabel"), AppStrings.OpenLanPort);
        AppStrings.Apply(F("lblPingLabel"),     AppStrings.PingLabel);
        AppStrings.Apply(F("lblTotalLabel"),    AppStrings.TotalLabel);
        AppStrings.Apply(F("lblDownloadLabel"), AppStrings.DownloadLabel);
        AppStrings.Apply(F("lblUploadLabel"),   AppStrings.UploadLabel);

        var btnConn = B("btnConnect");
        if (btnConn != null)
        {
            var txt = F("txtConnectBtn");
            if (txt != null)
            {
                bool connected = _state.IsConnected;
                txt.Text = connected
                    ? CrimsonOnion.Localization.AppStrings.ConnectedBtn
                    : CrimsonOnion.Localization.AppStrings.Connect;
                txt.FlowDirection = fa
                    ? global::Avalonia.Media.FlowDirection.RightToLeft
                    : global::Avalonia.Media.FlowDirection.LeftToRight;
            }
        }

        AppStrings.Apply(F("lblSectionStartup"), AppStrings.SectionStartup, forceLtr: true);
        AppStrings.Apply(F("lblLaunchOnStartup"),  AppStrings.LaunchOnStartup);
        AppStrings.Apply(F("lblAutoConnect"), AppStrings.AutoConnect);
        AppStrings.Apply(F("lblStartMinimized"), AppStrings.StartMinimized);
        AppStrings.Apply(F("lblMinimizeToTray"), AppStrings.MinimizeToTray);

        AppStrings.ApplyToolTip(F("lblLaunchOnStartup"), AppStrings.TtLaunchOnStartup);
        AppStrings.ApplyToolTip(F("lblAutoConnect"), AppStrings.TtAutoConnect);
        AppStrings.ApplyToolTip(F("lblStartMinimized"), AppStrings.TtStartMinimized);
        AppStrings.ApplyToolTip(F("lblMinimizeToTray"), AppStrings.TtMinimizeToTray);
        AppStrings.ApplyToolTip(B("btnRefreshPing"), AppStrings.TtPingRefresh);

        AppStrings.Apply(F("lblSectionConnection"), AppStrings.Connect, forceLtr: true);

        var tbLbPolicy = F("lblLbPolicy");
        AppStrings.Apply(tbLbPolicy, AppStrings.LbPolicy);
        AppStrings.ApplyToolTip(tbLbPolicy, AppStrings.TtLbPolicy);
        AppStrings.ApplyToolTip(ucSettings?.FindControl<Button>("btnLbPolicy"), AppStrings.TtLbPolicy);


        var tbCustomXray = F("lblCustomXrayExit");
        AppStrings.Apply(tbCustomXray, AppStrings.CustomXrayExit);
        AppStrings.ApplyToolTip(tbCustomXray, AppStrings.TtCustomXray);
        
        var tbOutboundProxy = F("lblOutboundProxySetting");
        AppStrings.Apply(tbOutboundProxy, AppStrings.OutboundProxy);
        AppStrings.ApplyToolTip(tbOutboundProxy, AppStrings.TtOutboundProxy);

        var tbAdapterBinding = F("lblAdapterBindingTitle");
        AppStrings.Apply(tbAdapterBinding, AppStrings.AdapterBinding);
        AppStrings.ApplyToolTip(tbAdapterBinding, AppStrings.TtAdapterBinding);
        AppStrings.ApplyBtn(B("btnScanAdapters"), AppStrings.ScanAdapters);
        
        var tbDnsSetting = F("lblDnsSettingTitle");
        AppStrings.Apply(tbDnsSetting, AppStrings.DnsSettings);
        AppStrings.ApplyToolTip(tbDnsSetting, AppStrings.TtDnsSettings);

        var tbAdBlocker = F("lblAdBlockerSetting");
        AppStrings.Apply(tbAdBlocker, AppStrings.AdBlocker);
        AppStrings.ApplyToolTip(tbAdBlocker, AppStrings.TtAdBlocker);
        
        var tbAllowLan = F("lblAllowLanSetting");
        AppStrings.Apply(tbAllowLan, AppStrings.AllowLan);
        AppStrings.ApplyToolTip(tbAllowLan, AppStrings.TtAllowLan);
        AppStrings.Apply(F("lblLanAuthTitle"), AppStrings.LanAuth);
        AppStrings.ApplyToolTip(F("lblLanAuthTitle"), AppStrings.TtLanAuth);

        AppStrings.Apply(F("lblOutboundType"), AppStrings.ProxyType);
        AppStrings.Apply(F("lblOutboundAddress"), AppStrings.AddressIp);
        AppStrings.Apply(F("lblOutboundPort"), AppStrings.Port);
        AppStrings.Apply(F("lblOutboundAuth"), AppStrings.Authentication);
        AppStrings.Apply(F("lblOutboundUsername"), AppStrings.Username);
        AppStrings.Apply(F("lblOutboundPassword"), AppStrings.Password);
        AppStrings.Apply(F("lblUpstreamDoh"), AppStrings.UpstreamDohUrl);
        AppStrings.Apply(F("lblSysDnsTitle"), AppStrings.SystemDns);
        AppStrings.ApplyToolTip(F("lblSysDnsTitle"), AppStrings.TtSystemDns);


        AppStrings.Apply(F("lblSectionSystem"),    AppStrings.SectionSystem, forceLtr: true);
        
        var tbLanguageSetting = F("lblLanguageSetting");
        AppStrings.Apply(tbLanguageSetting, AppStrings.LanguageSetting);
        AppStrings.ApplyToolTip(tbLanguageSetting, AppStrings.TtLanguage);
        
        var tbDebugMode = F("lblDebugMode");
        AppStrings.Apply(tbDebugMode, AppStrings.DebugMode);
        AppStrings.ApplyToolTip(tbDebugMode, AppStrings.TtDebugMode);
        
        AppStrings.Apply(F("lblDesktopShortcut"),  AppStrings.DesktopShortcut);
        AppStrings.Apply(F("lblStartMenuShortcut"), AppStrings.StartMenuShortcut);

        AppStrings.ApplyBtn(B("btnDesktopShortcut"), AppStrings.Create);
        AppStrings.ApplyBtn(B("btnStartMenuShortcut"), AppStrings.Create);

        AppStrings.Apply(F("lblSplitTunnelingHeader"), AppStrings.SplitTunneling, forceLtr: true);
        AppStrings.Apply(F("lblDomainsAndIps"), AppStrings.DomainsAndIps);
        AppStrings.Apply(F("lblApplications"), AppStrings.Applications);
        var lblSplitAppsWarning = F("lblSplitAppsWarning");
        if (lblSplitAppsWarning != null) lblSplitAppsWarning.Text = AppStrings.LblSplitAppsWarning;
        AppStrings.Apply(F("lblBlockedDomainsIps"), AppStrings.BlockedDomains);
        AppStrings.Apply(F("lblDirectUdpHeader"), AppStrings.SplitTunnelDirectUDP);
        AppStrings.ApplyToolTip(F("lblDirectUdpHeader"), AppStrings.SplitTunnelDirectUDPTooltip);
        AppStrings.Apply(F("lblDirectUdpDesc"), AppStrings.SplitTunnelDirectUDPDesc);
        
        ucSplitTunnel?.UpdateSplitTunnelUI();

        var btnSplitDisabled  = B("btnSplitDisabled");
        var btnSplitExclusive = B("btnSplitExclusive");
        var btnSplitInclusive = B("btnSplitInclusive");
        
        AppStrings.ApplyToolTip(btnSplitDisabled, AppStrings.TtSplitDis);
        AppStrings.ApplyToolTip(btnSplitExclusive, AppStrings.TtSplitExc);
        AppStrings.ApplyToolTip(btnSplitInclusive, AppStrings.TtSplitInc);
        
        if (btnSplitDisabled?.Content  is TextBlock tbDis) AppStrings.Apply(tbDis, AppStrings.Disabled);
        if (btnSplitExclusive?.Content is TextBlock tbEx)  AppStrings.Apply(tbEx, AppStrings.Exclusive);
        if (btnSplitInclusive?.Content is TextBlock tbIn)  AppStrings.Apply(tbIn, AppStrings.Inclusive);

        var btnToggleDomains = B("btnToggleDomains");
        var btnToggleApps    = B("btnToggleApps");
        var btnToggleBlock   = B("btnToggleBlock");
        if (btnToggleDomains != null) 
            btnToggleDomains.Content = string.IsNullOrWhiteSpace(T("txtSplitDomains")?.Text) ? AppStrings.Add : AppStrings.Edit;
        if (btnToggleApps    != null) 
            btnToggleApps.Content    = string.IsNullOrWhiteSpace(T("txtSplitApps")?.Text) ? AppStrings.Add : AppStrings.Edit;
        if (btnToggleBlock   != null) 
            btnToggleBlock.Content   = string.IsNullOrWhiteSpace(T("txtSplitBlock")?.Text) ? AppStrings.Add : AppStrings.Edit;

        var btnBrowseApp = B("btnBrowseApp");
        if (btnBrowseApp != null) btnBrowseApp.Content = AppStrings.BtnBrowse;

        AppStrings.Apply(F("lblAboutVersion"),  AppStrings.AboutVersion);
        AppStrings.Apply(F("lblAboutCreator"),  AppStrings.AboutCreator);
        AppStrings.Apply(F("lblAboutLicense"),  AppStrings.AboutLicense);
        AppStrings.Apply(F("lblDonations"), AppStrings.DonationsTitle);
        AppStrings.Apply(F("lblDonationsDesc"), AppStrings.DonationsDesc);
        AppStrings.ApplyBtn(B("btnCheckUpdate"), AppStrings.CheckForUpdates);

        AppStrings.Apply(F("lblExpertTitle"), AppStrings.ExpertTitle);
        
        AppStrings.ApplyBtn(B("btnExpertSave"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnExpertCancel"), AppStrings.BtnCancel);
        AppStrings.ApplyBtn(B("btnXraySave"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnXrayCancel"), AppStrings.BtnCancel);
        AppStrings.ApplyBtn(B("btnOutboundSave"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnOutboundCancel"), AppStrings.BtnCancel);
        AppStrings.ApplyBtn(B("btnDohSave"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnSysDnsSave"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnLanAuthSave"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnSaveDomains"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnCancelDomains"), AppStrings.BtnCancel);
        AppStrings.ApplyBtn(B("btnSaveApps"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnCancelApps"), AppStrings.BtnCancel);
        AppStrings.ApplyBtn(B("btnSaveBlock"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnCancelBlock"), AppStrings.BtnCancel);
        
        AppStrings.ApplyBtn(B("btnCaptchaSubmit"), AppStrings.Submit);
        AppStrings.ApplyBtn(B("btnCaptchaCancel"), AppStrings.BtnCancel);
        AppStrings.ApplyBtn(B("btnCustomSave"), AppStrings.Save);
        AppStrings.ApplyBtn(B("btnCustomCancel"), AppStrings.BtnCancel);

        AppStrings.Apply(F("lblCountriesOptimized"), AppStrings.RoutingOptimized);
        AppStrings.Apply(F("lblCountriesExpert"), AppStrings.RoutingExpert);

        foreach (var c in Countries) c.UpdateLanguage();
        ApplyRoutingUI(false);

        if (_trayWidget != null)
            _trayWidget.ApplyLanguage(fa);
            
        UpdateLanPortUI();
        UpdateLocalPortUI();
        
        ucSettings?.TriggerUpdateAdapterBindingMutualExclusivity();
        ApplyModeUI(_cfg.LastXrayMode);

        var txtConnectBtn = F("txtConnectBtn");
        var txtConnectedBtn = F("txtConnectedBtn");
        if (_state.IsConnected)
        {
            if (txtConnectedBtn != null) txtConnectedBtn.Text = AppStrings.ConnectedBtn;
            if (txtConnectBtn != null) txtConnectBtn.Text = AppStrings.ConnectedBtn;
        }
        else if (_state.IsEngineRunning)
        {
            if (txtConnectBtn != null) txtConnectBtn.Text = AppStrings.BtnConnecting;
        }
        else
        {
            if (txtConnectBtn != null) txtConnectBtn.Text = AppStrings.Connect;
        }
    }

    private async void SidebarCountries_Click(object? sender, RoutedEventArgs e)
    {
        bool isSelf = CountriesPopup != null && CountriesPopup.IsOpen && CountriesPopup.PlacementTarget?.Name == "SidebarBorder";
        if (isSelf)
        {
            _ = ClosePopupAnimatedAsync();
            return;
        }

        if (CountriesPopup != null && CountriesPopup.IsOpen)
        {
            CountriesPopup.IsOpen = false;
            if (CountriesPopup.Child is Border oldBorder) oldBorder.Classes.Remove("popupOpen");
        }

        _ = ClosePopupAnimatedAsync();
        CloseAllOverlays();

        if (CountriesPopup != null)
        {
            CountriesPopup.PlacementTarget  = this.FindControl<Control>("SidebarBorder");
            CountriesPopup.Placement        = PlacementMode.RightEdgeAlignedTop;
            CountriesPopup.HorizontalOffset = 10;
            CountriesPopup.VerticalOffset   = 0;
            CountriesPopup.IsOpen           = true;
            await Task.Delay(10);
            if (CountriesPopup.Child is Border border) border.Classes.Add("popupOpen");
            LightDismissOverlay.IsVisible   = true;
        }
    }

    private async void MainCountries_Click(object? sender, RoutedEventArgs e)
    {
        bool isSelf = CountriesPopup != null && CountriesPopup.IsOpen && CountriesPopup.PlacementTarget?.Name == "btnCurrentCountry";
        if (isSelf)
        {
            _ = ClosePopupAnimatedAsync();
            return;
        }

        if (CountriesPopup != null && CountriesPopup.IsOpen)
        {
            CountriesPopup.IsOpen = false;
            if (CountriesPopup.Child is Border oldBorder) oldBorder.Classes.Remove("popupOpen");
        }

        _ = ClosePopupAnimatedAsync();
        CloseAllOverlays();

        if (CountriesPopup != null)
        {
            CountriesPopup.PlacementTarget  = this.FindControl<Control>("btnCurrentCountry");
            CountriesPopup.Placement        = PlacementMode.Bottom;
            CountriesPopup.HorizontalOffset = 0;
            CountriesPopup.VerticalOffset   = 5;
            CountriesPopup.IsOpen           = true;
            await Task.Delay(10);
            if (CountriesPopup.Child is Border border) border.Classes.Add("popupOpen");
            LightDismissOverlay.IsVisible   = true;
        }
    }

    private void Mode_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button clickedBtn)
        {
            if (clickedBtn.Name == "btnVpnMode" && _activeBridge == "snowflake" && !_cfg.EnableDirectUDP) return;

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

    private async void UpdateAdvancedBridgesUI(bool forceHide = false)
    {
        var panAdvancedBridges = this.FindControl<global::Avalonia.Controls.Border>("panAdvancedBridges");
        var btnAmpCacheMode = B("btnAmpCacheMode");
        var panDnsRegDiv = this.FindControl<global::Avalonia.Controls.Border>("panDnsRegDiv");
        var btnDnsRegMode = B("btnDnsRegMode");
        
        if (panAdvancedBridges != null && panDnsRegDiv != null && btnDnsRegMode != null && btnAmpCacheMode != null)
        {
            bool show = !forceHide && (_activeBridge == "snowflake" || _activeBridge == "conjure");
            bool isConjure = (_activeBridge == "conjure");

            void ApplyState()
            {
                btnAmpCacheMode.Classes.Add("notrans");
                btnDnsRegMode.Classes.Add("notrans");

                if (isConjure)
                {
                    if (_cfg.EnableConjureAmpCache) btnAmpCacheMode.Classes.Add("activeMode");
                    else btnAmpCacheMode.Classes.Remove("activeMode");

                    if (_cfg.EnableConjureDnsRegistration) btnDnsRegMode.Classes.Add("activeMode");
                    else btnDnsRegMode.Classes.Remove("activeMode");
                }
                else
                {
                    if (_cfg.EnableSnowflakeAmpCache) btnAmpCacheMode.Classes.Add("activeMode");
                    else btnAmpCacheMode.Classes.Remove("activeMode");
                }
                panDnsRegDiv.IsVisible = isConjure;
                btnDnsRegMode.IsVisible = isConjure;
                btnAmpCacheMode.SetValue(global::Avalonia.Controls.Grid.ColumnSpanProperty, isConjure ? 1 : 2);
            }

            async void FinishState()
            {
                await System.Threading.Tasks.Task.Delay(50); 
                btnAmpCacheMode.Classes.Remove("notrans");
                btnDnsRegMode.Classes.Remove("notrans");
            }

            if (show)
            {
                if (panAdvancedBridges.Opacity > 0 && panDnsRegDiv.IsVisible != isConjure)
                {
                    panAdvancedBridges.Opacity = 0.0;
                    await System.Threading.Tasks.Task.Delay(150);
                    ApplyState();
                    panAdvancedBridges.Opacity = 1.0;
                    FinishState();
                }
                else
                {
                    ApplyState();
                    panAdvancedBridges.Opacity = 1.0;
                    FinishState();
                }
                panAdvancedBridges.IsHitTestVisible = true;
            }
            else
            {
                panAdvancedBridges.Opacity = 0.0;
                panAdvancedBridges.IsHitTestVisible = false;
            }
        }
    }

    private void AdvancedBridgeMode_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;

        var btnAmpCacheMode = B("btnAmpCacheMode");
        var btnDnsRegMode = B("btnDnsRegMode");

        if (sender is global::Avalonia.Controls.Button clickedBtn)
        {
            if (clickedBtn.Classes.Contains("activeMode"))
            {
                clickedBtn.Classes.Remove("activeMode");
            }
            else
            {
                if (clickedBtn == btnAmpCacheMode)
                {
                    btnAmpCacheMode.Classes.Add("activeMode");
                    btnDnsRegMode?.Classes.Remove("activeMode");
                }
                else if (clickedBtn == btnDnsRegMode)
                {
                    btnDnsRegMode.Classes.Add("activeMode");
                    btnAmpCacheMode?.Classes.Remove("activeMode");
                }
            }
        }

        if (_activeBridge == "snowflake")
        {
            _cfg.EnableSnowflakeAmpCache = btnAmpCacheMode?.Classes.Contains("activeMode") ?? false;
        }
        else if (_activeBridge == "conjure")
        {
            _cfg.EnableConjureAmpCache = btnAmpCacheMode?.Classes.Contains("activeMode") ?? false;
            _cfg.EnableConjureDnsRegistration = btnDnsRegMode?.Classes.Contains("activeMode") ?? false;
        }
        
        RequestConfigSave();
        
        if (_state.IsEngineRunning)
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
        }
    }

    private void Bridge_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button clickedBtn)
        {
            string targetBridge = _activeBridge;
            if (clickedBtn.Name == "btnBridgeDirect")         targetBridge = "Direct";
            else if (clickedBtn.Name == "btnBridgeObfs4")     targetBridge = "obfs4";
            else if (clickedBtn.Name == "btnBridgeSnowflake") targetBridge = "snowflake";
            else if (clickedBtn.Name == "btnBridgeMeek")      targetBridge = "meek_lite";
            else if (clickedBtn.Name == "btnBridgeConjure")   targetBridge = "conjure";

            if (clickedBtn.Name != "btnBridgeCustom" && _activeBridge == targetBridge)
            {
                var customPan = this.FindControl<global::Avalonia.Controls.Border>("panCustomBridge");
                if (customPan != null && customPan.MaxHeight > 0)
                {
                    customPan.MaxHeight       = 0;
                    customPan.Opacity         = 0;
                    customPan.BorderThickness = new global::Avalonia.Thickness(0);

                    B("btnBridgeCustom")?.Classes.Remove("activeOpt");
                    clickedBtn.Classes.Add("activeOpt");
                }
                return;
            }

            B("btnBridgeDirect")?.Classes.Remove("activeOpt");
            B("btnBridgeObfs4")?.Classes.Remove("activeOpt");
            B("btnBridgeSnowflake")?.Classes.Remove("activeOpt");
            B("btnBridgeMeek")?.Classes.Remove("activeOpt");
            B("btnBridgeConjure")?.Classes.Remove("activeOpt");
            B("btnBridgeCustom")?.Classes.Remove("activeOpt");

            clickedBtn.Classes.Add("activeOpt");

            if (clickedBtn.Name == "btnBridgeCustom")
            {
                var customPan = this.FindControl<global::Avalonia.Controls.Border>("panCustomBridge");
                if (customPan != null)
                {
                    if (customPan.MaxHeight > 0)
                    {
                        btnCustomSave_Click(null!, null!);
                        return;
                    }
                    customPan.MaxHeight       = 500;
                    customPan.Opacity         = 1;
                    customPan.BorderThickness = new global::Avalonia.Thickness(1);
                    var txtCustom = this.FindControl<global::Avalonia.Controls.TextBox>("txtCustomBridge");
                    if (txtCustom != null) txtCustom.Text = _cfg?.CustomBridgeLine;
                    UpdateAdvancedBridgesUI(true);
                }
                return;
            }

            if (clickedBtn.Name == "btnBridgeDirect")         _activeBridge = "Direct";
            else if (clickedBtn.Name == "btnBridgeObfs4")     _activeBridge = "obfs4";
            else if (clickedBtn.Name == "btnBridgeSnowflake") _activeBridge = "snowflake";
            else if (clickedBtn.Name == "btnBridgeMeek")      _activeBridge = "meek_lite";
            else if (clickedBtn.Name == "btnBridgeConjure")   _activeBridge = "conjure";

            _cfg.LastBridge = _activeBridge;
            RequestConfigSave();
            
            UpdateAdvancedBridgesUI();

            if (_activeBridge == "snowflake" && _cfg.LastXrayMode == "VPN Mode" && !_cfg.EnableDirectUDP)
            {
                _cfg.LastXrayMode = "Proxy Mode";
                _pollMode         = "Proxy Mode";
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastVpnDisabledSnowflake);
            }

            if (_activeBridge == "snowflake" && _cfg.EnableAdapterBinding)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastAdapterBindingSnowflake);
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
                    var txtCustomBridge = T("txtCustomBridge");
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
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
        }
    }

    private async void Engines_ValueChanged(object? sender, global::Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
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
                await OnEngineCountChanged(engines);
            else
            {
                UpdateDisconnectedTorLabels();
                RequestConfigSave();
            }
        }
    }


    private void ApplyLoadedSettings()
    {
        B("btnBridgeDirect")?.Classes.Remove("activeOpt");
        B("btnBridgeObfs4")?.Classes.Remove("activeOpt");
        B("btnBridgeSnowflake")?.Classes.Remove("activeOpt");
        B("btnBridgeMeek")?.Classes.Remove("activeOpt");
        B("btnBridgeConjure")?.Classes.Remove("activeOpt");
        B("btnBridgeCustom")?.Classes.Remove("activeOpt");

        if (_activeBridge == "obfs4")          B("btnBridgeObfs4")?.Classes.Add("activeOpt");
        else if (_activeBridge == "snowflake") B("btnBridgeSnowflake")?.Classes.Add("activeOpt");
        else if (_activeBridge == "meek_lite") B("btnBridgeMeek")?.Classes.Add("activeOpt");
        else if (_activeBridge == "conjure")   B("btnBridgeConjure")?.Classes.Add("activeOpt");
        else if (_activeBridge == "Custom")    B("btnBridgeCustom")?.Classes.Add("activeOpt");
        else                                   B("btnBridgeDirect")?.Classes.Add("activeOpt");

        var txtCustomBridge = this.FindControl<global::Avalonia.Controls.TextBox>("txtCustomBridge");
        if (txtCustomBridge != null) txtCustomBridge.Text = _cfg.CustomBridgeLine;

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

        var togDirectUDP = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togDirectUDP");
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

        ApplyModeUI(_pollMode);
        ApplyRoutingUI(false);
        UpdateLanPortUI();
        UpdateAdvancedBridgesUI();

        var langLbl = F("lblCurrentLanguage");
        if (langLbl != null) langLbl.Text = _cfg.Language;
        ApplyLanguage();

        var lbLbl = F("lblCurrentLbPolicy");
        if (lbLbl != null)
        {
            lbLbl.Text = _cfg.HaProxyBalancePolicy switch
            {
                "leastload"  => "LEAST LOAD",
                "leastping"  => "LEAST PING",
                "roundrobin" => "ROUND ROBIN",
                "random"     => "RANDOM",
                "leastconn"  => "LEAST LOAD",
                "first"      => "ROUND ROBIN",
                _            => (_cfg.HaProxyBalancePolicy ?? "leastping").ToUpperInvariant()
            };
        }
    }

    private void ApplyModeUI(string mode)
    {
        this.FindControl<global::Avalonia.Controls.Button>("btnProxyMode")?.Classes.Remove("activeMode");
        this.FindControl<global::Avalonia.Controls.Button>("btnVpnMode")?.Classes.Remove("activeMode");
        this.FindControl<global::Avalonia.Controls.Button>("btnClearProxy")?.Classes.Remove("activeMode");

        var panVpnMode = this.FindControl<global::Avalonia.Controls.Panel>("panVpnMode");
        var btnVpnMode = this.FindControl<global::Avalonia.Controls.Button>("btnVpnMode");
        if (btnVpnMode != null)
        {
            if (_activeBridge == "snowflake" && !_cfg.EnableDirectUDP)
            {
                btnVpnMode.IsEnabled = false;
                btnVpnMode.Opacity = 0.3;
                AppStrings.ApplyToolTip(panVpnMode, AppStrings.TtDisabledVpnSnowflake);
            }
            else
            {
                btnVpnMode.IsEnabled = true;
                btnVpnMode.Opacity = 1.0;
                if (panVpnMode != null) global::Avalonia.Controls.ToolTip.SetTip(panVpnMode, null);
            }
        }

        if (mode == "VPN Mode")       
            this.FindControl<global::Avalonia.Controls.Button>("btnVpnMode")?.Classes.Add("activeMode");
        else if (mode == "Clear Proxy") 
            this.FindControl<global::Avalonia.Controls.Button>("btnClearProxy")?.Classes.Add("activeMode");
        else                           
            this.FindControl<global::Avalonia.Controls.Button>("btnProxyMode")?.Classes.Add("activeMode");
            
        ucSplitTunnel?.UpdateSplitTunnelUI();
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

        // Unsubscribe AppMessenger to prevent leaks after window is gone.
        CrimsonOnion.Services.AppMessenger.ToastRequested -= (msg, success) => ShowToast(msg, success);

        // ── Stop & null all timers ──────────────────────────────────────────
        _glowTimer?.Stop();         _glowTimer        = null;
        _autoBootTimer?.Stop();     _autoBootTimer    = null;
        _bootstrapTimer?.Stop();    _bootstrapTimer   = null;
        _staggerTimer?.Stop();      _staggerTimer     = null;
        _xrayBootTimer?.Stop();     _xrayBootTimer    = null;
        _xrayRestartTimer?.Stop();  _xrayRestartTimer = null;
        _sessionClockTimer?.Stop(); _sessionClockTimer = null;
        _saveDebounceTimer?.Stop(); _saveDebounceTimer = null;
        _toastTimer?.Stop();        _toastTimer       = null;
        _fillAnimTimer?.Stop();     _fillAnimTimer    = null;
        _graphTimer?.Stop();        _graphTimer       = null;
        _logTimer?.Stop();          _logTimer         = null;
        _logClearTimer?.Stop();     _logClearTimer    = null;

        // ── Cancel & dispose all CancellationTokenSources ──────────────────
        if (_statsCts != null) { try { _statsCts.Cancel(); _statsCts.Dispose(); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _statsCts = null; }
        if (_pingCts  != null) { try { _pingCts.Cancel();  _pingCts.Dispose();  } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _pingCts  = null; }
        if (_geoCts   != null) { try { _geoCts.Cancel();   _geoCts.Dispose();   } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _geoCts   = null; }

        // ── Misc resources ──────────────────────────────────────────────────
        try { _httpClient?.Dispose(); _httpClient = null; } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }
        try { _cts?.Dispose();        _cts        = null; } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }

        StopAllEngines(isClosing: true);
        foreach (var country in Countries)
            country.Flag?.Dispose();
        DisposeTrayIcon();
    }


    private void SidebarBorder_PointerEntered(object? sender, PointerEventArgs e)
    {
        var panLeftStats = this.FindControl<Grid>("panLeftStats");
        if (panLeftStats != null)
        {
            panLeftStats.Opacity = 0.05; 
            panLeftStats.IsHitTestVisible = false;
        }
        var rectAllThemes = this.FindControl<global::Avalonia.Controls.Border>("rectAllThemes");
        if (rectAllThemes != null) rectAllThemes.Opacity = 1.0;
        var panCurrentThemeIcon = this.FindControl<global::Avalonia.Controls.Panel>("panCurrentThemeIcon");
        if (panCurrentThemeIcon != null) { panCurrentThemeIcon.Opacity = 0.0; }
    }

    internal void ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName)) themeName = "Crimson";
        global::Avalonia.Media.SolidColorBrush accent = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#B82E42"));
        global::Avalonia.Media.SolidColorBrush accentHover = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#D13A51"));
        global::Avalonia.Media.SolidColorBrush accentPressed = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#932535"));
        global::Avalonia.Media.Color glow = global::Avalonia.Media.Color.Parse("#FFE64A62");
        switch (themeName)
        {
            case "Blue":
                accent = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#2B6CB0"));
                accentHover = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#3182CE"));
                accentPressed = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#2C5282"));
                glow = global::Avalonia.Media.Color.Parse("#63B3ED");
                break;
            case "Purple":
                accent = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#6B46C1"));
                accentHover = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#805AD5"));
                accentPressed = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#553C9A"));
                glow = global::Avalonia.Media.Color.Parse("#B794F4");
                break;
            case "Green":
                accent = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#2F855A"));
                accentHover = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#38A169"));
                accentPressed = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#276749"));
                glow = global::Avalonia.Media.Color.Parse("#68D391");
                break;
            case "Pink":
                accent = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#B83280"));
                accentHover = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#D53F8C"));
                accentPressed = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#97266D"));
                glow = global::Avalonia.Media.Color.Parse("#F687B3");
                break;
            case "Yellow":
                accent = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#B7791F"));
                accentHover = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#D69E2E"));
                accentPressed = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#975A16"));
                glow = global::Avalonia.Media.Color.Parse("#F6E05E");
                break;
            case "Crimson":
            default:
                break; 
        }

        if (global::Avalonia.Application.Current != null)
        {
            global::Avalonia.Application.Current.Resources["ThemeAccent"] = accent;
            global::Avalonia.Application.Current.Resources["ThemeAccentPointerOver"] = accentHover;
            global::Avalonia.Application.Current.Resources["ThemeAccentPressed"] = accentPressed;
            global::Avalonia.Application.Current.Resources["ThemeGlow"] = glow;
            global::Avalonia.Application.Current.Resources["ThemeGlowBrush"] = new global::Avalonia.Media.SolidColorBrush(glow);
            global::Avalonia.Application.Current.Resources["ToggleSwitchFillOn"] = accent;
            global::Avalonia.Application.Current.Resources["ToggleSwitchFillOnPointerOver"] = accentHover;
            global::Avalonia.Application.Current.Resources["ToggleSwitchFillOnPressed"] = accentPressed;
            global::Avalonia.Application.Current.Resources["SliderThumbBackground"] = accent;
            global::Avalonia.Application.Current.Resources["SliderThumbBackgroundPointerOver"] = accentHover;
            global::Avalonia.Application.Current.Resources["SliderThumbBackgroundPressed"] = accentPressed;
            global::Avalonia.Application.Current.Resources["SliderTrackValueFill"] = accent;
            global::Avalonia.Application.Current.Resources["SliderTrackValueFillPointerOver"] = accentHover;
            global::Avalonia.Application.Current.Resources["SliderTrackValueFillPressed"] = accentPressed;
        }

        if (_state != null && _state.IsEngineRunning)
        {
            var txtConnectBtn = this.FindControl<global::Avalonia.Controls.TextBlock>("txtConnectBtn");
            if (txtConnectBtn != null && txtConnectBtn.Text == CrimsonOnion.Localization.AppStrings.ConnectedBtn) 
                txtConnectBtn.Foreground = new global::Avalonia.Media.SolidColorBrush(glow);
        }

        if (this.Resources.ContainsKey($"Theme{themeName}Brush"))
        {
            this.Resources["ThemeCurrentBrush"] = this.Resources[$"Theme{themeName}Brush"];
        }
    }

    private void SidebarBorder_PointerExited(object? sender, global::Avalonia.Input.PointerEventArgs e)
    {
        var panLeftStats = this.FindControl<global::Avalonia.Controls.Grid>("panLeftStats");
        if (panLeftStats != null)
        {
            panLeftStats.Opacity = 1.0; 
            panLeftStats.IsHitTestVisible = true;
        }
        var rectAllThemes = this.FindControl<global::Avalonia.Controls.Border>("rectAllThemes");
        if (rectAllThemes != null) rectAllThemes.Opacity = 0.0;
        var panCurrentThemeIcon = this.FindControl<global::Avalonia.Controls.Panel>("panCurrentThemeIcon");
        if (panCurrentThemeIcon != null) { panCurrentThemeIcon.Opacity = 1.0; }
    }

        private void SidebarThemes_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panThemesOverlay = this.FindControl<global::Avalonia.Controls.Border>("panThemesOverlay");
        if (panThemesOverlay != null && !panThemesOverlay.IsVisible)
        {
            CloseAllOverlays();
            panThemesOverlay.IsVisible = true;
            panThemesOverlay.Classes.Add("popupOpen");
            var ldo = this.FindControl<global::Avalonia.Controls.Border>("LightDismissOverlay");
            if (ldo != null) ldo.IsVisible = true;
        }
    }

    private void ThemeSelect_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var btn = sender as global::Avalonia.Controls.Button;
        if (btn == null) return;
        
        string themeName = btn.CommandParameter?.ToString() ?? "Crimson";
        
        
        CloseAllOverlays();
        
        _cfg.ThemeColor = themeName;
        RequestConfigSave();

        global::Avalonia.Threading.DispatcherTimer.RunOnce(() => {
            ApplyTheme(themeName);
        }, System.TimeSpan.FromMilliseconds(300));
    }
}




