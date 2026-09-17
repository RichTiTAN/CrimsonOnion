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

using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input.Platform;
using CrimsonOnion.Services;

namespace CrimsonOnion;

public partial class MainWindow
{
    private bool _isInitializingSettings = false;
    private global::Avalonia.Threading.DispatcherTimer? _saveDebounceTimer;
    private global::Avalonia.Threading.DispatcherTimer? _toastTimer;

    private GeoResult? _lastGeo = null;
    private bool _geoTimedOut = false;


    private static readonly SolidColorBrush BrGray   = new SolidColorBrush(Color.FromRgb(160, 174, 192)); // #A0AEC0
    private static readonly SolidColorBrush _brGreenFallback = new SolidColorBrush(Color.FromRgb(104, 211, 145));
    internal static SolidColorBrush BrGreen
    {
        get
        {
            if (global::Avalonia.Application.Current?.Resources.TryGetValue("ThemeGlowBrush", out var res) == true && res is SolidColorBrush b)
                return b;
            return _brGreenFallback;
        }
    }
    private static readonly SolidColorBrush BrOrange = new SolidColorBrush(Color.FromRgb(246, 173, 85));  // #F6AD55
    private static readonly SolidColorBrush BrRed    = new SolidColorBrush(Color.FromRgb(245, 101, 101)); // #F56565
    private static readonly SolidColorBrush BrPink   = new SolidColorBrush(Color.FromRgb(252, 129, 129)); // #FC8181


    private readonly GraphScroller _graphScroll;

    private System.Windows.Forms.NotifyIcon? _trayIcon;

    private void RequestConfigSave()
    {
        if (_saveDebounceTimer == null)
        {
            _saveDebounceTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveDebounceTimer.Tick += (s, e) => { _saveDebounceTimer.Stop(); SaveConfig(); };
        }
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void SaveConfig()
    {
        ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
    }

    private string GetAppPath(string relPath)
    {
        return Path.Combine(_cfg.BaseDir, relPath);
    }
    private void StartDnsttTunnels(int torIdx, IReadOnlyList<CrimsonOnion.Services.DnsttProxyArgs> proxies)
    {
        Action<string>? logger = null;
        if (_cfg.DebugMode) logger = line => AppendDebugLine($"dnstt{torIdx}", line);

        CrimsonOnion.Services.DnsttManager.StartTunnels(proxies, GetAppPath(@"Data\TorBin"), _cfg, logger);
    }
    private void AppendDebugLine(string tag, string line)
    {
        try
        {
            using (var fs = new System.IO.FileStream(GetAppPath("debug.log"), System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite, 4096, false))
            using (var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
            {
                sw.WriteLine($"[{tag}] {line}");
            }
        }
        catch { }
    }

    // ── Process management — delegated to VpnEngineService ──────────────────

    private int? StartDebugProcess(string exePath, string args, string workingDir, string label, bool warnOnly = true)
        => CrimsonOnion.Services.VpnEngineService.StartDebugProcess(exePath, args, workingDir, label, warnOnly);

    private async Task UpdateLanIpAsync()
    {
        _state.LanIp = await CrimsonOnion.Services.NetworkDiagnosticsService.GetLanIpAsync(_cfg).ConfigureAwait(false);
    }

    private void UpdateLocalPortUI()
    {
        if (lblLocalIp == null) return;

        lblLocalIp.ClearValue(global::Avalonia.Controls.TextBlock.ForegroundProperty);

        if (_state.IsConnected || _state.IsEngineRunning)
        {
            lblLocalIp.Text = "127.0.0.1:10818";
        }
        else
        {
            lblLocalIp.Text = CrimsonOnion.Localization.AppStrings.Disconnected;
        }
    }

    private void UpdateLanPortUI()
    {
        var lblLanIp = this.FindControl<global::Avalonia.Controls.TextBlock>("lblLanIp");
        if (lblLanIp == null) return;

        lblLanIp.ClearValue(global::Avalonia.Controls.TextBlock.ForegroundProperty);

        if (!_cfg.AllowLanConnections)
        {
            lblLanIp.Text = CrimsonOnion.Localization.AppStrings.Disabled;
        }
        else
        {
            if (_state.IsConnected || _state.IsEngineRunning)
            {
                lblLanIp.Text = (_state.LanIp ?? "UNKNOWN") + ":10818";
            }
            else
            {
                lblLanIp.Text = CrimsonOnion.Localization.AppStrings.Disconnected;
            }
        }
    }

    private global::Avalonia.Controls.TextBlock?[]? _torLabels;
    private void UpdateTorLabel(int torIdx)
    {
        if (torIdx < 1 || torIdx > 8) return;

        if (_torLabels == null)
        {
            _torLabels = new[] { lblTor1, lblTor2, lblTor3, lblTor4, lblTor5, lblTor6, lblTor7, lblTor8 };
        }

        var lbl = _torLabels[torIdx - 1];
        if (lbl == null) return;

        var (text, tone, opacity) = _torFarm.Describe(torIdx);

        lbl.Text       = text;
        lbl.Foreground = tone switch
        {
            TorSlotTone.Ready   => BrGreen,
            TorSlotTone.Warming => BrOrange,
            TorSlotTone.Error   => BrRed,
            _                   => BrGray,
        };
        lbl.Opacity = opacity;
    }

    private void OnTorSlotChanged(int torIdx)
        => Dispatcher.UIThread.Post(() => UpdateTorLabel(torIdx));

    private void OnTorProgressUpdated(int torIdx, int pct) => _torFarm.NoteProgress(torIdx, pct);

    private void OnTorConnectionDropped(int torIdx) => _torFarm.NoteDropped(torIdx);

    public void ShowToast(string message, bool success = false)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var toast = this.FindControl<Border>("ToastBorder");
            var toastText = this.FindControl<TextBlock>("ToastText");
            if (toast == null || toastText == null) return;

            _toastTimer?.Stop();

            bool isFa = CrimsonOnion.Localization.AppStrings.IsPersian;

            toastText.Text = isFa
                ? message
                : message.ToUpperInvariant();
            toastText.FontFamily = isFa
                ? new global::Avalonia.Media.FontFamily("Segoe UI")
                : global::Avalonia.Media.FontFamily.Default;
            toastText.FlowDirection = isFa
                ? global::Avalonia.Media.FlowDirection.RightToLeft
                : global::Avalonia.Media.FlowDirection.LeftToRight;
            toastText.FontWeight = global::Avalonia.Media.FontWeight.Bold;
            toastText.LetterSpacing = 1;
            toastText.Foreground = success ? BrGreen : BrPink;

            toast.Opacity = 0;
            toast.IsVisible = true;
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { toast.Opacity = 1; }, TimeSpan.FromMilliseconds(20));

            if (_toastTimer == null)
            {
                _toastTimer = new global::Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _toastTimer.Tick += (s, e) =>
                {
                    _toastTimer?.Stop();
                    if (toast != null)
                    {
                        toast.Opacity = 0;
                        global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { toast.IsVisible = false; }, TimeSpan.FromMilliseconds(300));
                    }
                };
            }
            _toastTimer.Start();
        });
    }

    private void OnToastRequested(string message, bool success) => ShowToast(message, success);

    // ── The connect flow ────────────────────────────────────────────────────
    internal void OnEngineCountChanged(int newCount) => _connect.EnginesChanged(newCount);

    private void StopAllEngines(bool isClosing = false) => _connect.Stop(isClosing);

    private void CopyIp_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        var tb = sender as TextBlock;
        if (tb != null && !string.IsNullOrWhiteSpace(tb.Text) && tb.Text.Contains(":") && !tb.Text.Contains("UNKNOWN"))
        {
            var clipboard = global::Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) _ = clipboard.SetTextAsync(tb.Text);

            string msg = CrimsonOnion.Localization.AppStrings.ToastCopiedToClipboard;
            ShowToast(msg, success: true);
        }
    }

    private void RefreshLocation_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        RefreshGeoAndPing();
    }

    private void RefreshPing_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        RefreshGeoAndPing();
    }

    private void RefreshGeoAndPing()
    {
        if (_state.IsConnected && !_state.IsGeoTracing)
        {
            StartGeoPing();
        }
    }

    private void btnConnect_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => _connect.ConnectClicked();
    private void SmartRestartXray() => _connect.RestartCoreForModeChange();

    private global::Avalonia.Controls.TextBlock? _lblTimerCache;
    private void StartSessionClock()
    {
        _bootstrap.StartSessionClock(() =>
        {
            if (!_state.IsConnected || _state.SessionStartTime == null)
            {
                _bootstrap.StopSessionClock();
                return;
            }
            var elapsed = DateTime.Now - _state.SessionStartTime.Value;
            if (_lblTimerCache == null) _lblTimerCache = this.FindControl<TextBlock>("lblTimer");
            if (_lblTimerCache != null)
                _lblTimerCache.Text = elapsed.ToString(@"hh\:mm\:ss");
        });
    }

    private global::Avalonia.Threading.DispatcherTimer? _logTimer;
    private global::Avalonia.Threading.DispatcherTimer? _logClearTimer;
    private readonly CrimsonOnion.Services.XrayLogTailer _logTailer = new();

    private void StartLogsTimers()
    {
        if (_logTimer != null)
        {
            _logTimer.Stop();
            _logTimer.Tick -= LogTimer_Tick;
        }
        _logTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _logTimer.Tick += LogTimer_Tick;
        _logTimer.Start();
        _logClearTimer?.Start();
    }

    private void StopLogsTimers()
    {
        if (_logTimer != null)
        {
            _logTimer.Stop();
            _logTimer.Tick -= LogTimer_Tick;
            _logTimer = null;
        }
        _logClearTimer?.Stop();
    }

    internal void InitLogClearTimer()
    {
        _logClearTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(2) };
        _logClearTimer.Tick += (s, e) => _xray.TruncateLogs();
    }

    private void chkLogs_CheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var panLogs = this.FindControl<global::Avalonia.Controls.Border>("panLogs");
        var chkLogs = sender as global::Avalonia.Controls.ToggleSwitch;
        if (panLogs != null && chkLogs != null)
        {
            if (chkLogs.IsChecked ?? false)
            {
                panLogs.MaxHeight       = 500;
                panLogs.Opacity         = 1;
                panLogs.BorderThickness = new global::Avalonia.Thickness(1);
                _state.IsLogsOpen       = true;
                StartLogsTimers();
            }
            else
            {
                panLogs.MaxHeight       = 0;
                panLogs.Opacity         = 0;
                panLogs.BorderThickness = new global::Avalonia.Thickness(0);
                _state.IsLogsOpen       = false;
                StopLogsTimers();
            }
            RequestConfigSave();
        }
    }

    private void LogTimer_Tick(object? sender, EventArgs e)
    {
        if (!_state.IsLogsOpen) return;
        _torFarm.RefreshSlots();

        if (!_state.IsEngineRunning)
        {
            var txtLogs = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayLogs");
            if (txtLogs != null) txtLogs.Text = "";
            return;
        }

        if (!_logTailer.TryBeginRead()) return;
        Task.Run(() =>
        {
            try
            {
                var newXrayLines = _logTailer.ReadNewLines(_xray.AccessLogPath);

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var txtXrayLogs = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayLogs");
                    if (newXrayLines.Length > 0 && txtXrayLogs != null)
                    {
                        _logTailer.Append(newXrayLines);
                        txtXrayLogs.Text = string.Join("\n", _logTailer.Lines);
                        txtXrayLogs.CaretIndex = txtXrayLogs.Text.Length;
                    }
                });
            }
            finally { _logTailer.EndRead(); }
        });
    }

    private void StartGeoPing()
    {
        _state.IsGeoTracing = true;
        _lastGeo = null;
        _geoTimedOut = false;

        var lblCountry = this.FindControl<TextBlock>("lblCountryName");
        var lblPing    = this.FindControl<TextBlock>("lblPing");
        if (lblCountry != null) lblCountry.Text = GeoDisplay.Describe(true, false, null, _cfg);
        if (lblPing    != null) lblPing.Text    = "0 ms";

        CancellationTokens.CancelAndDispose(ref _vpn.GeoCts);
        _vpn.GeoCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = _vpn.GeoCts.Token;

        _ = Task.Run(async () =>
        {
            var geo = await CrimsonOnion.Services.NetworkDiagnosticsService.FetchGeoAsync(token).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _state.IsGeoTracing = false;
                if (!_state.IsConnected)
                {
                    _lastGeo = null;
                    _geoTimedOut = false;
                    return;
                }

                if (geo == null)
                {
                    _geoTimedOut = true;
                    if (lblCountry != null) lblCountry.Text = GeoDisplay.Describe(false, true, null, _cfg);
                    if (lblPing    != null) lblPing.Text    = "0 ms";
                    return;
                }

                _lastGeo = geo;
                if (lblCountry != null) lblCountry.Text = GeoDisplay.Describe(false, false, geo, _cfg);

                if (lblPing    != null) lblPing.Text    = $"{geo.PingMs}ms";
            });
        }, token);
    }

    internal void RelocalizeGeo()
    {
        if (!_state.IsConnected) return;

        var lblCountry = this.FindControl<TextBlock>("lblCountryName");
        if (lblCountry == null) return;

        var text = GeoDisplay.Describe(_state.IsGeoTracing, _geoTimedOut, _lastGeo, _cfg);
        CrimsonOnion.Localization.AppStrings.Apply(lblCountry, text, forceLtr: true, keepFont: true);
    }

    private void StartStatsPolling()
    {
        UpdateLanPortUI();
        _logClearTimer?.Stop();
        _logClearTimer?.Start();

        _traffic.Start();
    }
    private void OnTrafficSampled(string up, string down, string total)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            var lblTotalData     = this.FindControl<TextBlock>("lblTotalData");
            var lblDownloadSpeed = this.FindControl<TextBlock>("lblDownloadSpeed");
            var lblUploadSpeed   = this.FindControl<TextBlock>("lblUploadSpeed");

            if (lblTotalData     != null) lblTotalData.Text     = total;
            if (lblDownloadSpeed != null) lblDownloadSpeed.Text = down;
            if (lblUploadSpeed   != null) lblUploadSpeed.Text   = up;

            DrawGraph();
        });
    }

    private global::Avalonia.Controls.Shapes.Path? _graphDownload;
    private global::Avalonia.Controls.Shapes.Path? _graphUpload;
    private global::Avalonia.Controls.Shapes.Path? _graphDownloadFill;
    private global::Avalonia.Controls.Shapes.Path? _graphUploadFill;
    private readonly System.Collections.Generic.List<global::Avalonia.Point> _ptsUpCache = new System.Collections.Generic.List<global::Avalonia.Point>(40);
    private readonly System.Collections.Generic.List<global::Avalonia.Point> _ptsDnCache = new System.Collections.Generic.List<global::Avalonia.Point>(40);

    private void DrawGraph()
    {
        if (_graphDownload == null)
        {
            _graphDownload = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphDownload");
            _graphUpload = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphUpload");
            _graphDownloadFill = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphDownloadFill");
            _graphUploadFill = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphUploadFill");
        }
        var graphDownload = _graphDownload;
        var graphUpload = _graphUpload;
        var graphDownloadFill = _graphDownloadFill;
        var graphUploadFill = _graphUploadFill;

        if (graphUpload == null || graphDownload == null || graphUploadFill == null || graphDownloadFill == null) return;

        const double width  = 150;
        const double height = 40;
        const double topPadding = 4;
        const double bottomPadding = 2;

        var (count, step) = CrimsonOnion.Services.SplineGeometry.BuildSeries(
            _traffic.Up, _traffic.Down, _ptsUpCache, _ptsDnCache,
            CrimsonOnion.Services.TrafficStatsService.HistorySize, width, height, topPadding, bottomPadding);

        if (count < 2) return;

        graphUpload.Data = CrimsonOnion.Services.SplineGeometry.Smooth(_ptsUpCache, false, width, height);
        graphDownload.Data = CrimsonOnion.Services.SplineGeometry.Smooth(_ptsDnCache, false, width, height);
        graphUploadFill.Data = CrimsonOnion.Services.SplineGeometry.Smooth(_ptsUpCache, true, width, height);
        graphDownloadFill.Data = CrimsonOnion.Services.SplineGeometry.Smooth(_ptsDnCache, true, width, height);

        var canvas = graphUpload.Parent as global::Avalonia.Controls.Canvas;
        if (canvas != null && canvas.RenderTransform is global::Avalonia.Media.TranslateTransform t)
        {
            _graphScroll.Restart(t, step);
        }
    }

    private CrimsonOnion.Dialogs.TrayWidget? _trayWidget;

    internal void InitTrayIcon()
    {
        using var iconStream = global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://CrimsonOnion/Assets/icon.ico"));
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "CrimsonOnion",
            Icon = new System.Drawing.Icon(iconStream),
            Visible = true
        };

        _trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    WindowState = global::Avalonia.Controls.WindowState.Normal;
                    Show();
                    Activate();
                    Topmost = true;
                    Topmost = false;
                });
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_trayWidget != null)
                    {
                        _trayWidget.Close();
                        _trayWidget = null;
                    }
                    else
                    {
                        _trayWidget = new CrimsonOnion.Dialogs.TrayWidget(this);

                        var pt = System.Windows.Forms.Cursor.Position;
                        int width = 220;
                        int height = 195;

                        _trayWidget.Position = new global::Avalonia.PixelPoint(pt.X - (width / 2), pt.Y - height - 10);

                        _trayWidget.Closed += (ws, we) => { _trayWidget = null; };
                        _trayWidget.Show();
                        _trayWidget.Activate();
                    }
                });
            }
        };
    }

    internal void DisposeTrayIcon()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Icon?.Dispose();
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    private void RoutingOption_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Button btn && btn.Tag is string tag)
        {
            _ = _overlays.ClosePopupsAsync();

            if (tag == "Expert")
            {
                ucExpert?.LoadFromConfig(_cfg);

                _overlays.ShowPane(Views.OverlayNavigation.ExpertPane);
                _overlays.ClosePane(Views.OverlayNavigation.SplitTunnelPane);
                _overlays.ClosePane(Views.OverlayNavigation.SettingsPane);
            }
            else
            {
                _cfg.LastConfig = tag;
                RequestConfigSave();

                ApplyRoutingUI();
            }
        }
    }

    internal void TriggerExpertSave(Views.Overlays.ExpertOverlay pane)
    {
        pane.SaveOntoConfig(_cfg);

        _cfg.LastConfig = "Expert";
        RequestConfigSave();

        _overlays.CloseAll();

        ApplyRoutingUI();
    }

    internal void TriggerOpenThemesPane() => _overlays.OpenPane(Views.OverlayNavigation.ThemesPane);

    internal void TriggerOpenSplitTunnelPane() => _overlays.OpenPane(Views.OverlayNavigation.SplitTunnelPane);

    internal void TriggerOpenSettingsPane() => _overlays.OpenPane(Views.OverlayNavigation.SettingsPane);

    internal void TriggerOpenAboutPane() => _overlays.OpenPane(Views.OverlayNavigation.AboutPane);

    internal void TriggerToggleSidebarCountriesPopup() => _ = _overlays.TogglePopupAsync(Views.OverlayNavigation.SidebarCountriesPopup);

    private void ApplyRoutingUI(bool showToast = true)
    {
        if (_cfg.LastConfig == "Optimized")
        {
            txtCurrentRouting.Text = CrimsonOnion.Localization.AppStrings.RoutingOptimized;
            iconCurrentRouting.Data = global::Avalonia.Media.Geometry.Parse("M7 2v11h3v9l7-12h-4l4-8z");
        }
        else if (_cfg.LastConfig == "Expert")
        {
            txtCurrentRouting.Text = CrimsonOnion.Localization.AppStrings.RoutingExpert;
            iconCurrentRouting.Data = global::Avalonia.Media.Geometry.Parse("M19.43 12.98c.04-.32.06-.64.06-.98s-.02-.66-.06-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.49 1c-.52-.4-1.08-.73-1.69-.98l-.38-2.65C14.46 2.18 14.25 2 14 2h-4c-.25 0-.46.18-.49.42l-.38 2.65c-.61.25-1.17.59-1.69.98l-2.49-1c-.23-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64l2.11 1.65c-.04.32-.06.65-.06.98s.02.66.06.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1c.52.4 1.08.73 1.69.98l.38 2.65c.03.24.24.42.49.42h4c.25 0 .46-.18.49-.42l.38-2.65c.61-.25 1.17-.59 1.69-.98l2.49 1c.23.09.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65zM12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5z");
        }
        else
        {
            var country = Countries.FirstOrDefault(c => c.Tag == _cfg.LastConfig);
            txtCurrentRouting.Text = country != null ? country.Name.ToUpper() : "CUSTOM";
            iconCurrentRouting.Data = global::Avalonia.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z");
        }

        if (_state.IsEngineRunning && showToast)
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
        }
    }

    private string?   _savedDnsAdapterName;
    private string[]? _savedDnsServers;

    private async Task ApplySystemDnsAsync()
    {
        if (!_cfg.EnableSystemDns) return;
        if (string.IsNullOrWhiteSpace(_cfg.SystemDnsPrimary)) return;

        await Task.Run(() =>
        {
            try
            {
                System.Net.NetworkInformation.NetworkInterface? nic = null;
                if (_cfg.EnableAdapterBinding && !string.IsNullOrWhiteSpace(_cfg.SelectedAdapterName))
                {
                    nic = SplitTunnelService.FindUpAdapter(_cfg.SelectedAdapterName);
                }

                if (nic == null)
                {
                    nic = DnsService.GetMainPhysicalAdapter();
                }

                if (nic == null)
                {
                    CrimsonOnion.Services.SimpleLogger.Log("[DnsService] No valid adapter found for DNS.");
                    return;
                }
                _savedDnsAdapterName = nic.Name;
                _savedDnsServers     = DnsService.GetCurrentDns(nic);

                DnsService.SetDns(nic.Name, _cfg.SystemDnsPrimary, _cfg.SystemDnsSecondary);
                CrimsonOnion.Services.SimpleLogger.Log($"[DnsService] Applied DNS {_cfg.SystemDnsPrimary}/{_cfg.SystemDnsSecondary} to {nic.Name}");
            }
            catch (Exception ex)
            {
                CrimsonOnion.Services.SimpleLogger.Log(ex);
            }
        });
    }

    private async Task RestoreSystemDnsAsync()
    {
        if (_savedDnsAdapterName == null) return;

        await Task.Run(() =>
        {
            try
            {
                DnsService.RestoreDns(_savedDnsAdapterName, _savedDnsServers ?? Array.Empty<string>());
                CrimsonOnion.Services.SimpleLogger.Log($"[DnsService] Restored DNS on {_savedDnsAdapterName}");
            }
            catch (Exception ex)
            {
                CrimsonOnion.Services.SimpleLogger.Log(ex);
            }
            finally
            {
                _savedDnsAdapterName = null;
                _savedDnsServers     = null;
            }
        });
    }
}



