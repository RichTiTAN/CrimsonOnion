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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Input.Platform;
using CrimsonOnion.Services;

namespace CrimsonOnion;

public partial class MainWindow
{
    private bool _isInitializingSettings = false;
    private global::Avalonia.Threading.DispatcherTimer? _saveDebounceTimer;
    private global::Avalonia.Threading.DispatcherTimer? _xrayRestartTimer;
    private global::Avalonia.Threading.DispatcherTimer? _sessionClockTimer;
    private global::Avalonia.Threading.DispatcherTimer? _toastTimer;
    private global::Avalonia.Threading.DispatcherTimer? _staggerTimer;
    private global::Avalonia.Threading.DispatcherTimer? _xrayBootTimer;
    private System.Collections.Generic.List<int> _staggerQueue = new();

    private global::Avalonia.Threading.DispatcherTimer? _statsTimer;
    private global::Avalonia.Threading.DispatcherTimer? _pingTimer;
    private static readonly System.Net.Http.HttpClient _geoPingClient = new System.Net.Http.HttpClient(
        new System.Net.Http.HttpClientHandler { Proxy = new System.Net.WebProxy("http://127.0.0.1:10818"), UseProxy = true })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    
    private static readonly System.Net.Http.HttpClient _grpcClient = new System.Net.Http.HttpClient
    {
        DefaultRequestVersion = new Version(2, 0),
        DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact
    };

    private static readonly SolidColorBrush BrGray   = new SolidColorBrush(Color.FromRgb(160, 174, 192)); // #A0AEC0
    private static readonly SolidColorBrush BrGreen  = new SolidColorBrush(Color.FromRgb(104, 211, 145)); // #68D391
    private static readonly SolidColorBrush BrOrange = new SolidColorBrush(Color.FromRgb(246, 173, 85));  // #F6AD55
    private static readonly SolidColorBrush BrRed    = new SolidColorBrush(Color.FromRgb(245, 101, 101)); // #F56565
    private static readonly SolidColorBrush BrWhite  = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #E2E8F0
    private static readonly SolidColorBrush BrAmber  = new SolidColorBrush(Color.FromRgb(221, 107, 32));  // #DD6B20
    private static readonly SolidColorBrush BrPink   = new SolidColorBrush(Color.FromRgb(252, 129, 129)); // #FC8181

    private static readonly System.Collections.Generic.Dictionary<string, string> _continentNames =
        new System.Collections.Generic.Dictionary<string, string>
        {
            ["NA"] = "NORTH AMERICA", ["EU"] = "EUROPE",  ["AS"] = "ASIA",
            ["SA"] = "SOUTH AMERICA", ["AF"] = "AFRICA",  ["OC"] = "OCEANIA", ["AN"] = "ANTARCTICA"
        };

    private System.Threading.CancellationTokenSource? _geoCts;
    private volatile bool _isFetchingStats = false; 
    private System.Collections.Generic.Queue<double> _upHistory = new();
    private System.Collections.Generic.Queue<double> _dnHistory = new();
    private double _upSum = 0;
    private double _dnSum = 0;
    private long _lastUpBytes = 0;
    private long _lastDnBytes = 0;


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

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private void KillPidRef(ref int? pidRef)
    {
        if (pidRef.HasValue)
        {
            try
            {
                using var p = Process.GetProcessById(pidRef.Value);
                if (!p.HasExited) { p.Kill(); p.WaitForExit(1000); }
            }
            catch { }
            pidRef = null;
        }
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
                        if (paths.Any(path => string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase))
                            || exePath.IndexOf(@"Data\Tors\", StringComparison.OrdinalIgnoreCase) >= 0)
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

    private void UpdateLanIp()
    {
        try
        {
            var ip = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                          && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                          && !ua.Address.ToString().StartsWith("127.")
                          && !ua.Address.ToString().StartsWith("169.254."))
                .Select(ua => ua.Address.ToString())
                .FirstOrDefault();
            _state.LanIp = ip ?? "UNKNOWN";
        }
        catch { _state.LanIp = "UNKNOWN"; }
    }

    private void UpdateLocalPortUI()
    {
        var lblLocalIp = this.FindControl<global::Avalonia.Controls.TextBlock>("lblLocalIp");
        if (lblLocalIp == null) return;
        
        lblLocalIp.ClearValue(global::Avalonia.Controls.TextBlock.ForegroundProperty);
        
        if (_state.IsConnected || _state.IsEngineRunning)
        {
            lblLocalIp.Text = "127.0.0.1:10818";
        }
        else
        {
            lblLocalIp.Text = CrimsonOnion.Localization.AppStrings.PortStatusDisconnected;
        }
    }

    private void UpdateLanPortUI()
    {
        var lblLanIp = this.FindControl<global::Avalonia.Controls.TextBlock>("lblLanIp");
        if (lblLanIp == null) return;

        lblLanIp.ClearValue(global::Avalonia.Controls.TextBlock.ForegroundProperty);

        if (!_cfg.AllowLanConnections)
        {
            lblLanIp.Text = CrimsonOnion.Localization.AppStrings.PortStatusDisabled;
        }
        else
        {
            if (_state.IsConnected || _state.IsEngineRunning)
            {
                lblLanIp.Text = (_state.LanIp ?? "UNKNOWN") + ":10818";
            }
            else
            {
                lblLanIp.Text = CrimsonOnion.Localization.AppStrings.PortStatusDisconnected;
            }
        }
    }



    private void UpdateRingAnimation(string state)
    {
        var panConnectGlow = this.FindControl<global::Avalonia.Controls.Border>("panConnectGlow");
        if (panConnectGlow != null)
        {
            panConnectGlow.Opacity = (state == "Connecting") ? 1.0 : 0.0;
        }
    }

    internal void UpdateDisconnectedTorLabels()
    {
        for (int i = 1; i <= 8; i++)
        {
            var lbl = this.FindControl<TextBlock>($"lblTor{i}");
            if (lbl != null)
            {
                var padded = i.ToString().PadLeft(2, '0');
                if (i > _activeTorEngines)
                {
                    lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusDisabled}";
                    lbl.Foreground = BrGray;
                    lbl.Opacity = 0.5;
                }
                else
                {
                    lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusOffline}";
                    lbl.Foreground = BrGray;
                    lbl.Opacity = 0.5;
                }
            }
        }
    }

    private void UpdateTorLabel(int torIdx)
    {
        if (torIdx < 1 || torIdx > 8) return;
        TextBlock? lbl = torIdx switch
        {
            1 => lblTor1,
            2 => lblTor2,
            3 => lblTor3,
            4 => lblTor4,
            5 => lblTor5,
            6 => lblTor6,
            7 => lblTor7,
            8 => lblTor8,
            _ => null
        };
        if (lbl == null) return;

        var padded = torIdx.ToString().PadLeft(2, '0');

        int uiSelCount = _activeTorEngines;
        if (torIdx > uiSelCount && torIdx > _pollSelCount)
        {
            lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusDisabled}";
            lbl.Foreground = BrGray;
            lbl.Opacity = 0.5;
            return;
        }
        else if (torIdx > _pollSelCount && torIdx <= uiSelCount)
        {
            lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusOffline}";
            lbl.Foreground = BrGray;
            lbl.Opacity = 0.5;
            return;
        }

        int pct = _state.TorPcts[torIdx - 1];
        if (pct == -2)
        {
            lbl.Text = $"TOR {padded}: ERR";
            lbl.Foreground = BrRed;
            lbl.Opacity = 1.0;
        }
        else if (pct >= 0)
        {
            lbl.Text = $"TOR {padded}: {(pct == 100 ? "100%" : $"{pct}%")}";
            lbl.Foreground = pct == 100 ? BrGreen : BrOrange;
            lbl.Opacity = 1.0;
        }
        else
        {
            lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusBooting}";
            lbl.Foreground = BrGray;
            lbl.Opacity = 1.0;
        }
    }


    public void ShowToast(string message, bool success = false)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var toast = this.FindControl<Border>("ToastBorder");
            var toastText = this.FindControl<TextBlock>("ToastText");
            if (toast == null || toastText == null) return;

            _toastTimer?.Stop();

            toastText.Text = CrimsonOnion.Localization.AppStrings.IsPersian
                ? message
                : message.ToUpperInvariant();
            toastText.FontFamily = CrimsonOnion.Localization.AppStrings.IsPersian
                ? new global::Avalonia.Media.FontFamily("Segoe UI")
                : global::Avalonia.Media.FontFamily.Default;
            toastText.FlowDirection = CrimsonOnion.Localization.AppStrings.IsPersian
                ? global::Avalonia.Media.FlowDirection.RightToLeft
                : global::Avalonia.Media.FlowDirection.LeftToRight;
            toastText.FontWeight = global::Avalonia.Media.FontWeight.Bold;
            toastText.LetterSpacing = 1;
            toastText.Foreground = success ? BrGreen : BrPink;

            toast.Opacity = 0;
            toast.IsVisible = true;
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { toast.Opacity = 1; }, TimeSpan.FromMilliseconds(20));

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
            _toastTimer.Start();
        });
    }





    internal void OnEngineCountChanged(int newCount)
    {
        RequestConfigSave();

        if (_state.IsConnected)
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
            return;
        }

        if (!_state.IsEngineRunning) return;

        int curCount = _pollSelCount;

        if (newCount < curCount)
        {
            _pollSelCount = newCount;
            for (int i = newCount + 1; i <= 8; i++)
            {
                KillPidRef(ref _torPids[i - 1]);
                _staggerQueue.Remove(i);
                UpdateTorLabel(i);
            }
            FormatHAProxyConfig(newCount);
        }
        else if (newCount > curCount)
        {
            _pollSelCount = newCount;
            FormatHAProxyConfig(newCount);

            bool isBootstrapping = _bootstrapTimer?.IsEnabled == true;

            for (int i = curCount + 1; i <= newCount; i++)
            {
                if (i >= 1 && i <= 8) _state.TorPcts[i - 1] = -1;

                var lbl = this.FindControl<TextBlock>($"lblTor{i}");
                if (lbl != null)
                {
                    lbl.Text       = $"TOR {i}: {CrimsonOnion.Localization.AppStrings.TorStatusWaiting}";
                    lbl.Foreground = BrGray;
                }

                TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));
                TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\Data\control_auth_cookie"));

                if (isBootstrapping) _staggerQueue.Add(i);
            }

            if (isBootstrapping && _staggerQueue.Count > 0)
            {
                if (_staggerTimer?.IsEnabled != true)
                {
                    _staggerTimer = new global::Avalonia.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(1500)
                    };
                    _staggerTimer.Tick += (s, e) =>
                    {
                        if (_state.AbortBoot || !_state.IsEngineRunning || _staggerQueue.Count == 0)
                        {
                            _staggerTimer?.Stop();
                            return;
                        }
                        int idx = _staggerQueue[0];
                        _staggerQueue.RemoveAt(0);
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

        TryDeleteFile(Path.Combine(torPath, "tor.log"));
        TryDeleteFile(Path.Combine(torPath, "Data", "control_auth_cookie"));

        if (!Directory.Exists(torPath)) Directory.CreateDirectory(torPath);

        var lines = TorrcBuilder.BuildTorrcConfig(torrcFile, _pollSelBridge, _cfg.LastConfig, torPath, _cfg);
        File.WriteAllLines(Path.Combine(torPath, torrcFile), lines);

        using (var proc = ProcessService.StartProcessDirect(
            GetAppPath(@"Data\TorBin\tor.exe"),
            $"-f {torrcFile}",
            torPath,
            hidden: !_cfg.DebugMode))
        {
        if (proc != null)
        {
            _torPids[i - 1] = proc.Id;

            var existing = _torControlClients.FirstOrDefault(c => c.TorIndex == i);
            if (existing != null)
            {
                existing.Dispose();
                _torControlClients.Remove(existing);
            }

            var controlClient = new TorControlClient(
                20050 + i,
                Path.Combine(torPath, "Data", "control_auth_cookie"),
                i);

            controlClient.BootstrapProgressUpdated += (torIdx, pct) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (torIdx >= 1 && torIdx <= 8 && _state.TorPcts[torIdx - 1] != 100)
                    {
                        _state.TorPcts[torIdx - 1] = Math.Max(_state.TorPcts[torIdx - 1], pct);
                        UpdateTorLabel(torIdx);
                    }
                });
            };

            controlClient.ConnectionDropped += (torIdx) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (torIdx >= 1 && torIdx <= 8)
                    {
                        if (_state.TorPcts[torIdx - 1] != 100)
                        {
                            _state.TorPcts[torIdx - 1] = -2; 
                            UpdateTorLabel(torIdx);
                        }
                    }
                });
            };

            _torControlClients.Add(controlClient);
            controlClient.Start();
            
            UpdateTorLabel(i);
        }
        }
    }


    private void StopAllEngines(bool isClosing = false)
    {
        _state.AbortBoot       = true;
        _state.IsEngineRunning = false;
        _bootstrapTimer?.Stop();
        _staggerTimer?.Stop();
        _staggerQueue.Clear();
        _sessionClockTimer?.Stop();
        _statsTimer?.Stop();
        _pingTimer?.Stop();
        if (_geoCts != null) { try { _geoCts.Cancel(); _geoCts.Dispose(); } catch { } _geoCts = null; } 
        _logTimer?.Stop(); 
        _logClearTimer?.Stop(); 
        ProxyService.SetSystemProxy(false);
        RestoreSystemDns();

        foreach (var client in _torControlClients) client.Dispose();
        _torControlClients.Clear();

        KillManagedProcess("tor");
        KillManagedProcess("haproxy");
        KillManagedProcess("xray");
        KillManagedProcess("sing-box");
        KillPidRef(ref _xrayDebugPid);
        KillPidRef(ref _sbDebugPid);
        KillPidRef(ref _adapterXrayDebugPid);


        _state.IsConnected      = false;
        _state.LastTotalBytes   = 0;
        _state.SessionDataBytes = 0;
        _state.SessionStartTime = null;
        _state.SpeedSamples     = new double[5];

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            _upHistory.Clear();
            _dnHistory.Clear();
            _upSum = 0;
            _dnSum = 0;
            var graphUpload = this.FindControl<global::Avalonia.Controls.Shapes.Polyline>("graphUpload");
            var graphDownload = this.FindControl<global::Avalonia.Controls.Shapes.Polyline>("graphDownload");
            if (graphUpload != null) graphUpload.Points = new global::Avalonia.Collections.AvaloniaList<global::Avalonia.Point>();
            if (graphDownload != null) graphDownload.Points = new global::Avalonia.Collections.AvaloniaList<global::Avalonia.Point>();

            var panTimerContent = this.FindControl<StackPanel>("panTimerContent");
            if (panTimerContent != null) panTimerContent.IsVisible = false;
            var lblDisconnected = this.FindControl<TextBlock>("lblDisconnected");
            if (lblDisconnected != null) lblDisconnected.IsVisible = true;

            var lblPing = this.FindControl<TextBlock>("lblPing");
            if (lblPing != null) lblPing.Text = "0 ms";

            UpdateLocalPortUI();
            UpdateLanPortUI();

            var lblTimer = this.FindControl<TextBlock>("lblTimer");
            if (lblTimer != null) lblTimer.Text = "00:00:00";
            var lblCountryName = this.FindControl<TextBlock>("lblCountryName");
            if (lblCountryName != null) lblCountryName.Text = "UNKNOWN";
        });

        for (int i = 1; i <= 8; i++)
        {
            TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));
            TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\Data\control_auth_cookie"));
        }
        TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
        TryDeleteFile(GetAppPath(@"Data\Xray\error.log"));
        TryDeleteFile(GetAppPath(@"Data\Xray\access.log.tmp"));

        if (!isClosing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (txtConnectBtn != null)
                {
                    txtConnectBtn.Text = CrimsonOnion.Localization.AppStrings.Connect;
                    txtConnectBtn.Foreground = BrWhite;
                }

                UpdateDisconnectedTorLabels();

                var txtXrayLogs = this.FindControl<TextBox>("txtXrayLogs");
                if (txtXrayLogs != null) txtXrayLogs.Text = "";

                var lblTot = this.FindControl<TextBlock>("lblTotalData");
                if (lblTot != null) lblTot.Text = "0 MB";
                var lblDn = this.FindControl<TextBlock>("lblDownloadSpeed");
                if (lblDn != null) lblDn.Text = "0 KB/s";
                var lblUp = this.FindControl<TextBlock>("lblUploadSpeed");
                if (lblUp != null) lblUp.Text = "0 KB/s";
                var lblPing = this.FindControl<TextBlock>("lblPing");
                if (lblPing != null) lblPing.Text = "0 ms";

                UpdateRingAnimation("Idle");
            });
        }
    }


    private void FormatHAProxyConfig(int activeCount)
    {
        var cfgPath = GetAppPath(@"Data\HAproxy\haproxy.cfg");
        if (!File.Exists(cfgPath)) return;
        var lines    = File.ReadAllLines(cfgPath).ToList();
        var newLines = new System.Collections.Generic.List<string>();

        foreach (var line in lines)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(line,
                @"^\s*(listen stats|bind 127\.0\.0\.1:10888|mode http|stats enable|stats uri /stats)"))
                continue;

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

        while (newLines.Count > 0 && string.IsNullOrWhiteSpace(newLines[newLines.Count - 1]))
            newLines.RemoveAt(newLines.Count - 1);

        if (!lines.SequenceEqual(newLines))
            File.WriteAllLines(cfgPath, newLines);
    }

    private void CopyIp_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        var tb = sender as TextBlock;
        if (tb != null && !string.IsNullOrWhiteSpace(tb.Text) && tb.Text.Contains(":") && !tb.Text.Contains("UNKNOWN"))
        {
            var clipboard = global::Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) _ = clipboard.SetTextAsync(tb.Text);
            
            string msg = CrimsonOnion.Localization.AppStrings.IsPersian ? "کپی شد!" : "Copied to clipboard!";
            ShowToast(msg, success: true);
        }
    }

    private void RefreshPing_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_state.IsConnected && !_state.IsGeoTracing)
        {
            StartGeoPing();
        }
    }

    // -------------------------------------------------------------------------------------------------

    private void btnConnect_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_state.IsConnected || _state.IsEngineRunning)
        {
            StopAllEngines();
            return;
        }

        if (!File.Exists(GetAppPath(@"Data\Tors\Tor1\Data\state")))
        {
            string msg = CrimsonOnion.Localization.AppStrings.IsPersian
                ? "اتصال اولیه ممکن است بیشتر طول بکشد، لطفاً صبر کنید."
                : "First connection might take longer, please wait.";
            ShowToast(msg);
        }

        if (_cfg.EnableAdapterBinding && !string.IsNullOrWhiteSpace(_cfg.SelectedAdapterName))
        {
            var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            bool exists = false;
            foreach (var adapter in adapters)
            {
                if (adapter.Name == _cfg.SelectedAdapterName && adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian ? "آداپتور انتخاب شده در دسترس نیست!" : "Selected adapter is not available!");
                return;
            }
        }

        StartEnginesAsync();
    }


    private async void StartEnginesAsync()
    {
        try
        {
        await StartEnginesAsyncCore();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartEnginesAsync failed: {ex.Message}\n{ex.StackTrace}");
            ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                ? $"خطا در شروع موتور: {ex.Message}"
                : $"Engine start failed: {ex.Message}");
            StopAllEngines();
        }
    }

    private async Task StartEnginesAsyncCore()
    {
        _state.IsEngineRunning = true;
        UpdateLanIp();

        ApplySystemDns();

        txtConnectBtn.Text = CrimsonOnion.Localization.AppStrings.IsPersian ? "در حال اتصال..." : "CONNECTING";
        txtConnectBtn.Foreground = BrAmber;
        UpdateRingAnimation("Connecting");

        foreach (var client in _torControlClients) client.Dispose();
        _torControlClients.Clear();
        for (int i = 0; i < 8; i++) KillPidRef(ref _torPids[i]);
        KillManagedProcess("tor");
        KillManagedProcess("haproxy");
        KillManagedProcess("xray");
        KillManagedProcess("sing-box");
        KillPidRef(ref _xrayDebugPid);
        KillPidRef(ref _sbDebugPid);
        KillPidRef(ref _adapterXrayDebugPid);

        _bootstrapTimer?.Stop();
        _staggerTimer?.Stop();
        _staggerQueue.Clear();
        ProxyService.SetSystemProxy(false);

        _state.IsConnected      = false;
        _state.LastTotalBytes   = 0;
        _state.SessionDataBytes = 0;
        _state.SessionStartTime = null;
        _state.SpeedSamples     = new double[5];
        _torPids                = new int?[8];
        _state.AbortBoot        = false;

        _pollSelCount  = _activeTorEngines;
        _pollSelBridge = _activeBridge;

        for (int i = 0; i < 8; i++) _state.TorPcts[i] = -1;

        for (int i = 1; i <= 8; i++) UpdateTorLabel(i);

        TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
        var txtXrayLogs = this.FindControl<TextBox>("txtXrayLogs");
        if (txtXrayLogs != null) txtXrayLogs.Text = "";
        _lastXrayLogPos = 0;
        _xrayLogLines.Clear();

        await Task.Delay(800);
        if (_state.AbortBoot) return;

        StartAdapterXray();

        for (int i = 1; i <= 8; i++)
            TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));

        FormatHAProxyConfig(_pollSelCount);

        for (int i = 1; i <= _pollSelCount; i++)
        {
            if (_state.AbortBoot) break;

            var torPath   = GetAppPath($@"Data\Tors\Tor{i}");
            var torrcFile = "torrc";

            if (!File.Exists(GetAppPath(@"Data\TorBin\tor.exe"))) continue;
            if (!Directory.Exists(torPath)) Directory.CreateDirectory(torPath);

            var lines = TorrcBuilder.BuildTorrcConfig(torrcFile, _pollSelBridge, _cfg.LastConfig, torPath, _cfg);
            File.WriteAllLines(Path.Combine(torPath, torrcFile), lines);

            var idx = i;
            TryDeleteFile(Path.Combine(torPath, "Data", "control_auth_cookie"));
            using (var proc = ProcessService.StartProcessDirect(
                        GetAppPath(@"Data\TorBin\tor.exe"),
                        $"-f {torrcFile}",
                        torPath,
                        hidden: !_cfg.DebugMode))
            {
            if (proc != null)
            {
                _torPids[idx - 1] = proc.Id;
                var controlClient = new TorControlClient(20050 + idx,
                    Path.Combine(torPath, "Data", "control_auth_cookie"), idx);
                controlClient.BootstrapProgressUpdated += (torIdx, pct) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_state.TorPcts[torIdx - 1] != 100)
                        {
                            _state.TorPcts[torIdx - 1] = Math.Max(_state.TorPcts[torIdx - 1], pct);
                            UpdateTorLabel(torIdx);
                        }
                    });
                };
                controlClient.ConnectionDropped += (torIdx) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (torIdx >= 1 && torIdx <= 8)
                        {
                            if (_state.TorPcts[torIdx - 1] != 100)
                            {
                                _state.TorPcts[torIdx - 1] = -2; 
                                UpdateTorLabel(torIdx);
                            }
                        }
                    });
                };
                _torControlClients.Add(controlClient);
                controlClient.Start();
                
                UpdateTorLabel(idx);
            }
            }

            await Task.Delay(1500);
        }

        if (_state.AbortBoot) return;

        FormatHAProxyConfig(_pollSelCount);

        bool isBridged  = _pollSelBridge != "Direct";
        int hardTimeout = isBridged ? 300 : 180;
        var deadline    = DateTime.Now.AddSeconds(hardTimeout);

        _bootstrapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _bootstrapTimer.Tick += (s, e) => BootstrapTick("Optimized", deadline);
        _bootstrapTimer.Start();
    }


    private void BootstrapTick(string selConfig, DateTime deadline)
    {
        if (_state.AbortBoot)
        {
            _bootstrapTimer?.Stop();
            if (txtConnectBtn != null)
            {
                txtConnectBtn.Text = CrimsonOnion.Localization.AppStrings.Connect;
                txtConnectBtn.Foreground = BrWhite;
            }
            UpdateRingAnimation("Idle");
            return;
        }

        bool oneReady   = false;
        int  bestPct    = -1;
        int  bestTorIdx = 1;

        for (int i = 1; i <= _pollSelCount; i++)
        {
            if (_state.TorPcts[i - 1] > bestPct)
            {
                bestPct    = _state.TorPcts[i - 1];
                bestTorIdx = i;
            }
            if (_state.TorPcts[i - 1] == 100) oneReady = true;
        }

        if (!oneReady)
        {
            if (DateTime.Now >= deadline)
            {
                _bootstrapTimer?.Stop();
                double elapsed = Math.Round((DateTime.Now - (_state.SessionStartTime ?? DateTime.Now)).TotalSeconds, 1);
                ShowToast($"Bootstrap timed out after {elapsed}s. Try a different bridge type.");
                StopAllEngines();
            }
            return;
        }

        _bootstrapTimer?.Stop();

        TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
        TryDeleteFile(GetAppPath(@"Data\Xray\error.log"));

        var haExe = GetAppPath(@"Data\HAproxy\haproxy.exe");
        if (File.Exists(haExe))
            ProcessService.StartProcessDirect(haExe, "-f haproxy.cfg", _cfg.HaPath, hidden: !_cfg.DebugMode)?.Dispose();
        _xrayBootTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _xrayBootTimer.Tick += (s2, e2) =>
        {
            _xrayBootTimer?.Stop();
            if (_state.AbortBoot) return;

            if (!XrayConfigWriter.Write(_cfg, _cfg.XrayDir)) return;

            if (_pollMode == "VPN Mode")
            {
                if (!SingboxConfigWriter.Write(_cfg, _cfg.SbDir)) return;
            }

            if (_cfg.DebugMode)
            {
                using var p = Process.Start(new ProcessStartInfo("cmd.exe",
                    $"/c \"title XrayDebug & .\\xray.exe run -c config.json || pause\"")
                    { WorkingDirectory = _cfg.XrayDir, UseShellExecute = true });
                _xrayDebugPid = p?.Id;
            }
            else
            {
                ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir)?.Dispose();
            }

            if (_pollMode == "VPN Mode")
            {
                if (_cfg.DebugMode)
                {
                    using var p2 = Process.Start(new ProcessStartInfo("cmd.exe",
                        $"/c \"title SingBoxDebug & .\\sing-box.exe run -c config.json || pause\"")
                        { WorkingDirectory = _cfg.SbDir, UseShellExecute = true });
                    _sbDebugPid = p2?.Id;
                }
                else
                {
                    ProcessService.StartProcessDirect(GetAppPath(@"Data\sing_box\sing-box.exe"), "run -c config.json", _cfg.SbDir)?.Dispose();
                }
            }

            ProxyService.SetSystemProxy(_pollMode == "Proxy Mode");

            _state.IsConnected      = true;
            _state.SessionStartTime = DateTime.Now;
            if (txtConnectBtn != null)
            {
                txtConnectBtn.Text = CrimsonOnion.Localization.AppStrings.ConnectedBtn;
                txtConnectBtn.Foreground = BrGreen;
            }
            UpdateRingAnimation("Connected");

            UpdateLanPortUI();

            UpdateLocalPortUI();

            StartSessionClock();
            StartGeoPing();
            StartStatsPolling();
            if (_state.IsLogsOpen) StartLogsTimers();
        };
        _xrayBootTimer.Start();
    }
    private int? _adapterXrayDebugPid;

    private void StartAdapterXray()
    {
        if (!_cfg.EnableAdapterBinding || string.IsNullOrWhiteSpace(_cfg.SelectedAdapterIp)) return;

        var adapterXrayDir = GetAppPath(@"Data\Xray");
        if (!Directory.Exists(adapterXrayDir)) Directory.CreateDirectory(adapterXrayDir);

        var configJson = $$"""
{
  "log": { "loglevel": "warning" },
  "inbounds": [
    {
      "port": 10819,
      "listen": "127.0.0.1",
      "protocol": "socks",
      "settings": { "auth": "noauth", "udp": true }
    }
  ],
  "outbounds": [
    {
      "protocol": "freedom",
      "settings": {},
      "sendThrough": "{{_cfg.SelectedAdapterIp}}"
    }
  ]
}
""";
        File.WriteAllText(Path.Combine(adapterXrayDir, "adapter_config.json"), configJson);
        
        if (_cfg.DebugMode)
        {
            using var p = Process.Start(new ProcessStartInfo("cmd.exe",
                $"/c \"title AdapterXrayDebug & .\\xray.exe run -c adapter_config.json || pause\"")
                { WorkingDirectory = adapterXrayDir, UseShellExecute = true });
            _adapterXrayDebugPid = p?.Id;
        }
        else
        {
            ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c adapter_config.json", adapterXrayDir, hidden: true)?.Dispose();
        }
    }

    private void SmartRestartXray()
    {
        if (_state.IsConnected)
        {
            if (_cfg.LastXrayMode == "VPN Mode")
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectSafely);
            else
                RestartXray(_cfg.LastXrayMode);
        }
        else if (_state.IsEngineRunning)
        {
            if (_cfg.LastXrayMode == "VPN Mode")
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
        }
    }

    private void RestartXray(string targetMode)
    {
        KillManagedProcess("xray");
        KillManagedProcess("sing-box");
        KillPidRef(ref _xrayDebugPid);
        KillPidRef(ref _sbDebugPid);


        _xrayRestartTimer?.Stop();
        _xrayRestartTimer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _xrayRestartTimer.Tick += (s, e) =>
        {
            _xrayRestartTimer?.Stop();
            if (!XrayConfigWriter.Write(_cfg, _cfg.XrayDir)) return;

            if (targetMode == "VPN Mode")
            {
                if (!SingboxConfigWriter.Write(_cfg, _cfg.SbDir)) return;
            }

            if (_cfg.DebugMode)
            {
                using var p = Process.Start(new ProcessStartInfo("cmd.exe",
                    $"/c \"title XrayDebug & .\\xray.exe run -c config.json || pause\"")
                    { WorkingDirectory = _cfg.XrayDir, UseShellExecute = true });
                _xrayDebugPid = p?.Id;
            }
            else
            {
                ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir)?.Dispose();
            }

            if (targetMode == "VPN Mode")
            {
                if (_cfg.DebugMode)
                {
                    using var p2 = Process.Start(new ProcessStartInfo("cmd.exe",
                        $"/c \"title SingBoxDebug & .\\sing-box.exe run -c config.json || pause\"")
                        { WorkingDirectory = _cfg.SbDir, UseShellExecute = true });
                    _sbDebugPid = p2?.Id;
                }
                else
                {
                    ProcessService.StartProcessDirect(GetAppPath(@"Data\sing_box\sing-box.exe"), "run -c config.json", _cfg.SbDir)?.Dispose();
                }
            }

            ProxyService.SetSystemProxy(targetMode == "Proxy Mode");

            if (_state.IsConnected)
            {
                UpdateLanIp();
                UpdateLanPortUI();

                UpdateLocalPortUI();

                _pingTimer?.Stop();
                var pt = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                pt.Tick += (s2, e2) => { pt.Stop(); StartGeoPing(); };
                _pingTimer = pt;
                pt.Start();
            }
        };
        _xrayRestartTimer.Start();
    }


    private void StartSessionClock()
    {

        var panTimerContent = this.FindControl<StackPanel>("panTimerContent");
        if (panTimerContent != null) panTimerContent.IsVisible = true;
        var lblDisconnected = this.FindControl<TextBlock>("lblDisconnected");
        if (lblDisconnected != null) lblDisconnected.IsVisible = false;

        _sessionClockTimer?.Stop();
        _sessionClockTimer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _sessionClockTimer.Tick += (s, e) =>
        {
            if (!_state.IsConnected || _state.SessionStartTime == null)
            {
                _sessionClockTimer?.Stop();
                return;
            }
            var elapsed = DateTime.Now - _state.SessionStartTime.Value;
            var lblTimer = this.FindControl<TextBlock>("lblTimer");
            if (lblTimer != null)
                lblTimer.Text = elapsed.ToString(@"hh\:mm\:ss");
        };
        _sessionClockTimer.Start();
    }


    private bool _fetchingBridges = false;
    private string _moatBridgeType = "";
    private string _moatChallengeId = "";
    private string _moatChallengeStr = "";
    private int _moatIndex = 0;
    private System.Net.Http.HttpClient? _httpClient;
    private System.Threading.CancellationTokenSource? _cts;
    private readonly string[] _moatEndpoints =
    {
        "https://bridges.torproject.org/moat",
        "https://bridges2.torproject.org/moat",
        "https://tor.eff.org/moat"
    };

    private void btnCustomSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtCustomBridge = this.FindControl<global::Avalonia.Controls.TextBox>("txtCustomBridge");
        if (_cfg != null && _state != null && txtCustomBridge != null)
        {
            _cfg.CustomBridgeLine = txtCustomBridge.Text ?? "";
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
        }
        var panCustomBridge = this.FindControl<global::Avalonia.Controls.Border>("panCustomBridge");
        if (panCustomBridge != null)
        {
            panCustomBridge.MaxHeight       = 0;
            panCustomBridge.Opacity         = 0;
            panCustomBridge.BorderThickness = new global::Avalonia.Thickness(0);
        }
    }

    private void btnCustomCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panCustomBridge = this.FindControl<global::Avalonia.Controls.Border>("panCustomBridge");
        if (panCustomBridge != null)
        {
            panCustomBridge.MaxHeight       = 0;
            panCustomBridge.Opacity         = 0;
            panCustomBridge.BorderThickness = new global::Avalonia.Thickness(0);
        }
        CancelFetch();
    }

    private void btnGetWebTunnel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => StartFetch("webtunnel");

    private void btnGetObfs4_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => StartFetch("obfs4");

    private void StartFetch(string bridgeType)
    {
        if (_fetchingBridges) { CancelFetch(); return; }
        _fetchingBridges = true;
        _moatBridgeType  = bridgeType;
        _moatIndex       = 0;

        var btnGetWebTunnel = this.FindControl<global::Avalonia.Controls.Button>("btnGetWebTunnel");
        var btnGetObfs4     = this.FindControl<global::Avalonia.Controls.Button>("btnGetObfs4");

        if (bridgeType == "webtunnel" && btnGetWebTunnel != null && btnGetObfs4 != null)
        {
            btnGetWebTunnel.Content  = "FETCHING...";
            btnGetObfs4.IsEnabled    = false;
        }
        else if (btnGetObfs4 != null && btnGetWebTunnel != null)
        {
            btnGetObfs4.Content        = "FETCHING...";
            btnGetWebTunnel.IsEnabled  = false;
        }

        _httpClient?.Dispose();
        try
        {
            var sysProxy = System.Net.WebRequest.GetSystemWebProxy();
            sysProxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            _httpClient = new System.Net.Http.HttpClient(
                new System.Net.Http.HttpClientHandler { Proxy = sysProxy, UseProxy = true });
        }
        catch
        {
            _httpClient = new System.Net.Http.HttpClient();
        }
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
        _ = RequestChallengeAsync();
    }

    private void CancelFetch()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _httpClient?.CancelPendingRequests();
        _httpClient?.Dispose();
        _httpClient = null;
        _fetchingBridges = false;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var btnGetWebTunnel  = this.FindControl<global::Avalonia.Controls.Button>("btnGetWebTunnel");
            var btnGetObfs4      = this.FindControl<global::Avalonia.Controls.Button>("btnGetObfs4");
            var btnCaptchaSubmit = this.FindControl<global::Avalonia.Controls.Button>("btnCaptchaSubmit");
            var panCaptcha       = this.FindControl<global::Avalonia.Controls.Border>("panCaptcha");

            if (btnGetWebTunnel != null) { btnGetWebTunnel.Content = "WEBTUNNEL"; btnGetWebTunnel.IsEnabled = true; }
            if (btnGetObfs4 != null)     { btnGetObfs4.Content     = "OBFS4";     btnGetObfs4.IsEnabled     = true; }
            if (btnCaptchaSubmit != null) { btnCaptchaSubmit.Content = CrimsonOnion.Localization.AppStrings.Submit; btnCaptchaSubmit.IsEnabled = true; }
            if (panCaptcha != null) { panCaptcha.MaxHeight = 0; panCaptcha.MaxWidth = 0; panCaptcha.Margin = new global::Avalonia.Thickness(0); panCaptcha.Opacity = 0; panCaptcha.BorderThickness = new global::Avalonia.Thickness(0); }
        });
    }

    private async Task RequestChallengeAsync()
    {
        for (_moatIndex = 0; _moatIndex < _moatEndpoints.Length; _moatIndex++)
        {
            if (!_fetchingBridges) return;

            var url  = _moatEndpoints[_moatIndex] + "/fetch";
            var body = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                data = new object[] { new { version = "0.1.0", type = "client-transports", supported = new[] { _moatBridgeType } } }
            });

            try
            {
                _cts?.Dispose();
                _cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));

                var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/vnd.api+json");
                using var response  = await _httpClient!.PostAsync(url, content, _cts.Token);
                var resultStr       = await response.Content.ReadAsStringAsync();

                if (!_fetchingBridges) return;

                var res = Newtonsoft.Json.Linq.JObject.Parse(resultStr);
                if (res["data"] is Newtonsoft.Json.Linq.JArray dataArr
                    && dataArr.Count > 0
                    && dataArr[0] is Newtonsoft.Json.Linq.JObject d0
                    && d0["id"] != null && d0["image"] != null && d0["challenge"] != null)
                {
                    _moatChallengeId  = d0["id"]!.ToString();
                    _moatChallengeStr = d0["challenge"]!.ToString();
                    var imgBytes      = Convert.FromBase64String(d0["image"]!.ToString());

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var panCaptcha      = this.FindControl<global::Avalonia.Controls.Border>("panCaptcha");
                        var imgCaptcha      = this.FindControl<global::Avalonia.Controls.Image>("imgCaptcha");
                        var txtCaptchaSol   = this.FindControl<global::Avalonia.Controls.TextBox>("txtCaptchaSol");
                        var btnCaptchaSubmit = this.FindControl<global::Avalonia.Controls.Button>("btnCaptchaSubmit");

                        if (imgCaptcha != null)
                        {
                            using var ms = new MemoryStream(imgBytes);
                            imgCaptcha.Source = new global::Avalonia.Media.Imaging.Bitmap(ms);
                        }
                        if (panCaptcha != null) { panCaptcha.MaxHeight = 300; panCaptcha.MaxWidth = 160; panCaptcha.Margin = new global::Avalonia.Thickness(0,0,10,0); panCaptcha.Opacity = 1; panCaptcha.BorderThickness = new global::Avalonia.Thickness(1); }
                        if (txtCaptchaSol != null) { txtCaptchaSol.Text = ""; txtCaptchaSol.Focus(); }
                        if (btnCaptchaSubmit != null) { btnCaptchaSubmit.Content = CrimsonOnion.Localization.AppStrings.Submit; btnCaptchaSubmit.IsEnabled = true; }
                    });
                    return;
                }
            }
            catch (Exception)
            {
                if (!_fetchingBridges) return;
            }
        }
        
        if (_fetchingBridges)
        {
            CancelFetch();
            _ = Dispatcher.UIThread.InvokeAsync(() => ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian ? "اتصال به سرورهای تور با مشکل مواجه شد.\nمیتوانید از ربات تلگرام زیر پل دریافت کنید:\n@GetBridgesBot" : "Failed to reach Tor servers. Please use the @GetBridgesBot Telegram bot or email bridges@torproject.org to get a bridge."));
        }
    }

    private void btnCaptchaCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => CancelFetch();

    private void btnCaptchaSubmit_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => _ = SubmitCaptchaAsync();

    private void txtCaptchaSol_KeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == global::Avalonia.Input.Key.Enter || e.Key == global::Avalonia.Input.Key.Return)
        {
            _ = SubmitCaptchaAsync();
            e.Handled = true;
        }
    }

    private async Task SubmitCaptchaAsync()
    {
        var txtCaptchaSol = this.FindControl<global::Avalonia.Controls.TextBox>("txtCaptchaSol");
        var solution = txtCaptchaSol?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(solution)) return;

        var btnCaptchaSubmit = this.FindControl<global::Avalonia.Controls.Button>("btnCaptchaSubmit");
        if (btnCaptchaSubmit != null) { btnCaptchaSubmit.Content = CrimsonOnion.Localization.AppStrings.CaptchaVerifying; btnCaptchaSubmit.IsEnabled = false; }

        if (_moatIndex >= _moatEndpoints.Length)
        {
            CancelFetch();
            return;
        }

        var url  = _moatEndpoints[_moatIndex] + "/check";
        var body = Newtonsoft.Json.JsonConvert.SerializeObject(new
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
            _cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
            var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/vnd.api+json");
            using var response = await _httpClient!.PostAsync(url, content, _cts.Token);
            var resultStr      = await response.Content.ReadAsStringAsync();

            if (!_fetchingBridges) return;

            var res = Newtonsoft.Json.Linq.JObject.Parse(resultStr);
            var dataArr = res["data"] as Newtonsoft.Json.Linq.JArray;
            if (dataArr != null && dataArr.Count > 0
                && dataArr[0]["bridges"] is Newtonsoft.Json.Linq.JArray bridges
                && bridges.Count > 0)
            {
                var lines = string.Join("\n", bridges.Select(b => b.ToString()));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var txtCustomBridge = this.FindControl<global::Avalonia.Controls.TextBox>("txtCustomBridge");
                    if (txtCustomBridge != null)
                    {
                        var existing = txtCustomBridge.Text?.Trim() ?? "";
                        txtCustomBridge.Text = string.IsNullOrWhiteSpace(existing) ? lines : $"{existing}\n{lines}";
                        txtCustomBridge.CaretIndex = txtCustomBridge.Text.Length;
                    }
                    CancelFetch();
                });
            }
            else
            {
                CancelFetch();
                _ = Dispatcher.UIThread.InvokeAsync(() => ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian ? "اتصال به سرورهای تور با مشکل مواجه شد.\nمیتوانید از ربات تلگرام زیر پل دریافت کنید:\n@GetBridgesBot" : "Failed to reach Tor servers. Please use the @GetBridgesBot Telegram bot or email bridges@torproject.org to get a bridge."));
            }
        }
        catch (Exception)
        {
            CancelFetch();
            _ = Dispatcher.UIThread.InvokeAsync(() => ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian ? "اتصال به سرورهای تور با مشکل مواجه شد.\nمیتوانید از ربات تلگرام زیر پل دریافت کنید:\n@GetBridgesBot" : "Failed to reach Tor servers. Please use the @GetBridgesBot Telegram bot or email bridges@torproject.org to get a bridge."));
        }
    }


    private global::Avalonia.Threading.DispatcherTimer? _logTimer;
    private global::Avalonia.Threading.DispatcherTimer? _logClearTimer;
    private long _lastXrayLogPos = 0;
    private volatile bool _isReadingLogs = false; 
    private readonly System.Collections.Generic.List<string> _xrayLogLines = new();

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
        _logClearTimer.Tick += (s, e) =>
        {
            foreach (var lf in new[] { @"Data\Xray\access.log", @"Data\Xray\error.log" })
            {
                var fp = GetAppPath(lf);
                if (File.Exists(fp))
                    try { using var fs = new FileStream(fp, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite); } catch { }
            }
        };
        _logClearTimer.Start();
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
        var selCount = _state.IsEngineRunning ? _activeTorEngines : int.TryParse(_cfg.LastCount, out int c) ? c : 1;

        if (!_state.IsEngineRunning)
        {
            for (int i = 1; i <= 8; i++)
            {
                var lbl = this.FindControl<global::Avalonia.Controls.TextBlock>($"lblTor{i}");
                if (lbl != null)
                {
                    var padded = i.ToString().PadLeft(2, '0');
                    lbl.Text = i <= selCount ? $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusOffline}" : $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusDisabled}";
                    lbl.Foreground = BrGray; 
                    lbl.Opacity = 0.5;
                }
            }
            var txtLogs = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayLogs");
            if (txtLogs != null) txtLogs.Text = "";
            return;
        }

        for (int i = 1; i <= 8; i++)
        {
            var lbl = this.FindControl<global::Avalonia.Controls.TextBlock>($"lblTor{i}");
            if (lbl != null)
            {
                var padded = i.ToString().PadLeft(2, '0');
                int uiSelCount = _activeTorEngines;
                if (i > uiSelCount && i > _pollSelCount)
                {
                    lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusDisabled}";
                    lbl.Foreground = BrGray;
                    lbl.Opacity = 0.5;
                }
                else if (i > _pollSelCount && i <= uiSelCount)
                {
                    lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusOffline}";
                    lbl.Foreground = BrGray;
                    lbl.Opacity = 0.5;
                }
                else
                {
                    if (_state.TorPcts[i - 1] == -1)
                    {
                        lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusBooting}";
                        lbl.Foreground = BrGray;
                        lbl.Opacity = 1.0;
                    }
                    else if (_state.TorPcts[i - 1] == 100)
                    {
                        lbl.Text = $"TOR {padded}: 100%";
                        lbl.Foreground = BrGreen;
                        lbl.Opacity = 1.0;
                    }
                    else
                    {
                        lbl.Text = $"TOR {padded}: {_state.TorPcts[i - 1]}%";
                        lbl.Foreground = BrOrange;
                        lbl.Opacity = 1.0;
                    }
                }
            }
        }

        if (_isReadingLogs) return;
        _isReadingLogs = true;
        Task.Run(() =>
        {
            try
            {
            var newXrayLines = new Queue<string>(16);
            var xrayLogPath = GetAppPath(@"Data\Xray\access.log");
            if (File.Exists(xrayLogPath))
            {
                try
                {
                    using var fs = new FileStream(xrayLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (fs.Length < _lastXrayLogPos) _lastXrayLogPos = 0;
                    if (fs.Length > _lastXrayLogPos)
                    {
                        fs.Seek(_lastXrayLogPos, SeekOrigin.Begin);
                        using var sr = new StreamReader(fs);
                        string? line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if ((line.Contains("accepted") || line.Contains("proxy")) && !line.Contains(":10899"))
                            {
                                int firstSpace = line.IndexOf(' ');
                                if (firstSpace > 0)
                                {
                                    int secondSpace = line.IndexOf(' ', firstSpace + 1);
                                    if (secondSpace > 0)
                                    {
                                        var span = line.AsSpan(secondSpace + 1);
                                        if (span.StartsWith("127.0.0.1:"))
                                        {
                                            int thirdSpace = span.IndexOf(' ');
                                            if (thirdSpace > 0) newXrayLines.Enqueue(span.Slice(thirdSpace + 1).ToString());
                                            else newXrayLines.Enqueue(span.ToString());
                                        }
                                        else newXrayLines.Enqueue(span.ToString());
                                        
                                        if (newXrayLines.Count > 15) newXrayLines.Dequeue();
                                    }
                                }
                            }
                        }
                        _lastXrayLogPos = fs.Length;
                    }
                }
                catch { }
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var txtXrayLogs = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayLogs");
                if (newXrayLines.Any() && txtXrayLogs != null)
                {
                    foreach (var l in newXrayLines) _xrayLogLines.Add(l);
                    if (_xrayLogLines.Count > 15) _xrayLogLines.RemoveRange(0, _xrayLogLines.Count - 15);
                    txtXrayLogs.Text = string.Join("\n", _xrayLogLines);
                    txtXrayLogs.CaretIndex = txtXrayLogs.Text.Length;
                }
            });
            }
            finally { _isReadingLogs = false; }
        });
    }


    private void chkStats_CheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panStats = this.FindControl<global::Avalonia.Controls.Border>("panStats");
        var chkStats = sender as global::Avalonia.Controls.ToggleSwitch;
        if (panStats != null && chkStats != null)
        {
            if (chkStats.IsChecked ?? false)
            {
                panStats.MaxHeight       = 100;
                panStats.Opacity         = 1;
                panStats.BorderThickness = new global::Avalonia.Thickness(1);
            }
            else
            {
                panStats.MaxHeight       = 0;
                panStats.Opacity         = 0;
                panStats.BorderThickness = new global::Avalonia.Thickness(0);
            }
        }
    }


    private void StartGeoPing()
    {
        if (_state.IsGeoTracing) return;
        _state.IsGeoTracing = true;

        var lblCountry = this.FindControl<TextBlock>("lblCountryName");
        var lblPing = this.FindControl<TextBlock>("lblPing");
        if (lblCountry != null) lblCountry.Text = CrimsonOnion.Localization.AppStrings.GeoTracing;
        if (lblPing != null) lblPing.Text = "0 ms";

        if (_geoCts != null) { try { _geoCts.Cancel(); _geoCts.Dispose(); } catch { } }
        _geoCts = new System.Threading.CancellationTokenSource();
        var token = _geoCts.Token;
        var sw    = Stopwatch.StartNew();

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _geoPingClient.GetStringAsync("https://get.geojs.io/v1/ip/geo.json", token);
                sw.Stop();
                var pingMs = sw.ElapsedMilliseconds;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _state.IsGeoTracing = false;
                    if (!_state.IsConnected) return;

                    var data = Newtonsoft.Json.Linq.JObject.Parse(result);

                    var cMap = _continentNames;
                    var continentCode = data["continent_code"]?.ToString() ?? "";
                    var countryCode   = data["country_code"]?.ToString() ?? "";
                    var continent     = cMap.TryGetValue(continentCode, out var c) ? c : continentCode;
                    var country       = data["country"]?.ToString() ?? "";

                    bool isFa = CrimsonOnion.Localization.AppStrings.IsPersian;
                    
                    if (isFa)
                    {
                        continent = CrimsonOnion.Localization.GeoTranslation.GetContinentFa(continentCode, continent);
                        country   = CrimsonOnion.Localization.GeoTranslation.GetCountryFa(countryCode, country);
                    }

                    string geoStr;
                    if (_cfg.EnableV2rayChain || _cfg.LastConfig == "Custom")
                        geoStr = country;
                    else if (_cfg.LastConfig == "Expert"
                             && !string.IsNullOrWhiteSpace(_cfg.ExpertExitNodes)
                             && !_cfg.ExpertExitNodes.Contains(","))
                        geoStr = country;
                    else if (_cfg.LastConfig != "Optimized" && _cfg.LastConfig != "Expert")
                        geoStr = country;
                    else
                        geoStr = continent;

                    if (lblCountry != null) lblCountry.Text = geoStr.ToUpper();
                    if (lblPing != null) lblPing.Text = $"{pingMs}ms";
                });
            }
            catch
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _state.IsGeoTracing = false;
                    if (!_state.IsConnected) return;

                    if (lblCountry != null) lblCountry.Text = CrimsonOnion.Localization.AppStrings.GeoTimeout;
                    if (lblPing != null) lblPing.Text = "0 ms";
                });
            }
        }, token);
    }


    private void StartStatsPolling()
    {
        UpdateLanPortUI();

        _statsTimer?.Stop();
        _statsTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _statsTimer.Tick += (s, e) => PollStatsTick();
        _statsTimer.Start();
    }

    private void PollStatsTick()
    {
        if (!_state.IsConnected || _isFetchingStats) return;
        _isFetchingStats = true;

        Task.Run(async () =>
        {
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Post,
                    "http://127.0.0.1:10899/xray.app.stats.command.StatsService/QueryStats")
                {
                    Version       = new Version(2, 0),
                    VersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionExact
                };
                request.Content = new System.Net.Http.ByteArrayContent(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x02, 0x0A, 0x00 });
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
                request.Headers.Add("TE", "trailers");

                using var cts       = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(1.5));
                using var response = await _grpcClient.SendAsync(request, cts.Token);
                var bytes          = await response.Content.ReadAsByteArrayAsync(cts.Token);

                long upVal = 0, dnVal = 0;
                int pos = 5;
                while (pos < bytes.Length)
                {
                    if (bytes[pos] == 0x0A)
                    {
                        pos++;
                        int statLen = ReadVarint(bytes, ref pos);
                        int statEnd = pos + statLen;
                        bool isUplink = false;
                        bool isDownlink = false;
                        long value = 0;
                        while (pos < statEnd)
                        {
                            int tag = ReadVarint(bytes, ref pos);
                            if (tag == 0x0A)
                            {
                                int nameLen = ReadVarint(bytes, ref pos);
                                var span = new ReadOnlySpan<byte>(bytes, pos, nameLen);
                                if (span.IndexOf("uplink"u8) >= 0) isUplink = true;
                                if (span.IndexOf("downlink"u8) >= 0) isDownlink = true;
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
                        if (isUplink)   upVal += value;
                        if (isDownlink) dnVal += value;
                    }
                    else break;
                }

                long curUpBytes = upVal;
                long curDnBytes = dnVal;

                if (curUpBytes > 0 && _lastUpBytes > 0)
                {
                    var diffUp = Math.Max(0, curUpBytes - _lastUpBytes);
                    var diffDn = Math.Max(0, curDnBytes - _lastDnBytes);
                    _state.SessionDataBytes += diffUp + diffDn;

                    _upSum += diffUp;
                    _upHistory.Enqueue(diffUp);
                    if (_upHistory.Count > 40) _upSum -= _upHistory.Dequeue();
                    
                    _dnSum += diffDn;
                    _dnHistory.Enqueue(diffDn);
                    if (_dnHistory.Count > 40) _dnSum -= _dnHistory.Dequeue();

                    var avgUp = (_upSum / (double)_upHistory.Count) * 2;
                    var avgDn = (_dnSum / (double)_dnHistory.Count) * 2;

                    string spdUp = avgUp >= 1048576 ? $"{Math.Round(avgUp / 1048576, 2)} MB/s"
                                 : avgUp >= 1024    ? $"{Math.Round(avgUp / 1024, 1)} KB/s"
                                 :                   $"{(int)avgUp} B/s";
                    string spdDn = avgDn >= 1048576 ? $"{Math.Round(avgDn / 1048576, 2)} MB/s"
                                 : avgDn >= 1024    ? $"{Math.Round(avgDn / 1024, 1)} KB/s"
                                 :                   $"{(int)avgDn} B/s";
                    string tot = _state.SessionDataBytes >= 1073741824
                                    ? $"{Math.Round(_state.SessionDataBytes / 1073741824.0, 2)} GB"
                               : _state.SessionDataBytes >= 1048576
                                    ? $"{Math.Round(_state.SessionDataBytes / 1048576.0, 1)} MB"
                               :     $"{Math.Round(_state.SessionDataBytes / 1024.0, 1)} KB";

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (lblTotalData != null) lblTotalData.Text = tot;
                        if (lblDownloadSpeed != null) lblDownloadSpeed.Text = spdDn;
                        if (lblUploadSpeed != null) lblUploadSpeed.Text = spdUp;
                        DrawGraph();
                    });
                }

                if (curUpBytes > 0) _lastUpBytes = curUpBytes;
                if (curDnBytes > 0) _lastDnBytes = curDnBytes;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Stats error: {ex.Message}"); }
            finally { _isFetchingStats = false; }
        });
    }


    private void DrawGraph()
    {
        if (graphUpload == null || graphDownload == null) return;

        const double width  = 150;
        const double height = 40;
        int count = Math.Min(_upHistory.Count, _dnHistory.Count);
        if (count < 2) return;

        double step   = width / (40 - 1);
        double maxUp  = _upHistory.Count > 0 ? _upHistory.Max() : 0;
        double maxDn  = _dnHistory.Count > 0 ? _dnHistory.Max() : 0;
        double maxVal = Math.Max(maxUp, maxDn);
        if (maxVal < 1024) maxVal = 1024;

        var ptsUp = new global::Avalonia.Collections.AvaloniaList<global::Avalonia.Point>();
        var ptsDn = new global::Avalonia.Collections.AvaloniaList<global::Avalonia.Point>();

        var upArr  = _upHistory.ToArray();
        var dnArr  = _dnHistory.ToArray();
        int startIdx = 40 - upArr.Length;

        for (int i = 0; i < Math.Min(upArr.Length, dnArr.Length); i++)
        {
            double x   = (startIdx + i) * step;
            double yUp = height - (upArr[i] / maxVal * height);
            double yDn = height - (dnArr[i] / maxVal * height);
            if (yUp < 2) yUp = 2;
            if (yDn < 2) yDn = 2;
            ptsUp.Add(new global::Avalonia.Point(x, yUp));
            ptsDn.Add(new global::Avalonia.Point(x, yDn));
        }

        graphUpload.Points   = ptsUp;
        graphDownload.Points = ptsDn;
    }


    private static int ReadVarint(byte[] data, ref int p)
    {
        int result = 0, shift = 0;
        while (p < data.Length)
        {
            byte b = data[p++];
            if (shift < 32) result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        return result;
    }

    private static long ReadVarint64(byte[] data, ref int p)
    {
        long result = 0; int shift = 0;
        while (p < data.Length)
        {
            byte b = data[p++];
            result |= (long)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        return result;
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
            _ = ClosePopupAnimatedAsync();

            if (tag == "Expert")
            {
                cmbHW.SelectedIndex = _cfg.ExpertHardwareAccel ? 1 : 0;
                cmbFF.SelectedIndex = _cfg.ExpertFascistFirewall ? 1 : 0;
                cmbSN.SelectedIndex = _cfg.ExpertStrictNodes ? 1 : 0;
                txtCBT.Text = _cfg.ExpertCircuitBuildTimeout;
                txtKP.Text = _cfg.ExpertKeepalivePeriod;
                txtNCP.Text = _cfg.ExpertNewCircuitPeriod;
                txtMCD.Text = _cfg.ExpertMaxCircuitDirtiness;
                txtNEG.Text = _cfg.ExpertNumEntryGuards;
                txtEN.Text = _cfg.ExpertEntryNodes;
                txtExit.Text = _cfg.ExpertExitNodes;
                txtExNodes.Text = _cfg.ExpertExcludeNodes;
                txtExExit.Text = _cfg.ExpertExcludeExitNodes;
                txtRaw.Text = _cfg.ExpertCustomTorrc;

                panExpertOverlay.IsVisible = true;
                panExpertOverlay.Classes.Add("popupOpen");
            var ldo = this.FindControl<global::Avalonia.Controls.Border>("LightDismissOverlay");
            if (ldo != null) ldo.IsVisible = true;
        var pSplitOv = this.FindControl<global::Avalonia.Controls.Border>("panSplitOverlay");
        if (pSplitOv != null && pSplitOv.IsVisible)
        {
            pSplitOv.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { pSplitOv.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }



                var panSettingsOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSettingsOverlay");
                if (panSettingsOverlay != null && panSettingsOverlay.IsVisible)
        {
            panSettingsOverlay.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panSettingsOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
            }
            else
            {
                _cfg.LastConfig = tag;
                RequestConfigSave();
                
                ApplyRoutingUI();
            }
        }
    }

    private void btnExpertSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cfg.ExpertHardwareAccel = cmbHW.SelectedIndex == 1;
        _cfg.ExpertFascistFirewall = cmbFF.SelectedIndex == 1;
        _cfg.ExpertStrictNodes = cmbSN.SelectedIndex == 1;
        _cfg.ExpertCircuitBuildTimeout = txtCBT.Text?.Trim() ?? "";
        _cfg.ExpertKeepalivePeriod = txtKP.Text?.Trim() ?? "";
        _cfg.ExpertNewCircuitPeriod = txtNCP.Text?.Trim() ?? "";
        _cfg.ExpertMaxCircuitDirtiness = txtMCD.Text?.Trim() ?? "";
        _cfg.ExpertNumEntryGuards = txtNEG.Text?.Trim() ?? "";
        _cfg.ExpertEntryNodes = txtEN.Text?.Trim() ?? "";
        _cfg.ExpertExitNodes = txtExit.Text?.Trim() ?? "";
        _cfg.ExpertExcludeNodes = txtExNodes.Text?.Trim() ?? "";
        _cfg.ExpertExcludeExitNodes = txtExExit.Text?.Trim() ?? "";
        _cfg.ExpertCustomTorrc = txtRaw.Text?.Trim() ?? "";
        
        _cfg.LastConfig = "Expert";
        RequestConfigSave();
        
        CloseAllOverlays();
        
        ApplyRoutingUI();
    }

    private void btnExpertCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseAllOverlays();
    }

    // ========================================================================

    private void btnSplitTunnel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panSplitOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSplitOverlay");
        if (panSplitOverlay != null)
        {
            panSplitOverlay.IsVisible = true;
            panSplitOverlay.Classes.Add("popupOpen");
            var ldo = this.FindControl<global::Avalonia.Controls.Border>("LightDismissOverlay");
            if (ldo != null) ldo.IsVisible = true;
        }
        var countriesPopup = this.FindControl<global::Avalonia.Controls.Primitives.Popup>("CountriesPopup");
        var languagePopup = this.FindControl<global::Avalonia.Controls.Primitives.Popup>("LanguagePopup");
        if ((countriesPopup != null && countriesPopup.IsOpen) || (languagePopup != null && languagePopup.IsOpen))
        {
            _ = ClosePopupAnimatedAsync();
        }
        var panSettingsOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSettingsOverlay");
        if (panSettingsOverlay != null && panSettingsOverlay.IsVisible)
        {
            panSettingsOverlay.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panSettingsOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        var panExpertOverlay = this.FindControl<global::Avalonia.Controls.Border>("panExpertOverlay");
        if (panExpertOverlay != null && panExpertOverlay.IsVisible)
        {
            panExpertOverlay.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panExpertOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        var panAboutOverlay = this.FindControl<global::Avalonia.Controls.Border>("panAboutOverlay");
        if (panAboutOverlay != null && panAboutOverlay.IsVisible)
        {
            panAboutOverlay.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panAboutOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
    }

    private void btnSplitClose_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panSplitOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSplitOverlay");
        if (panSplitOverlay != null)
        {
            panSplitOverlay.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panSplitOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
    }


    // ========================================================================

    private void btnSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        
        var panSettingsOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSettingsOverlay");
        if (panSettingsOverlay != null)
        {
            panSettingsOverlay.IsVisible = true;
            panSettingsOverlay.Classes.Add("popupOpen");
            var ldo = this.FindControl<global::Avalonia.Controls.Border>("LightDismissOverlay");
            if (ldo != null) ldo.IsVisible = true;
        var pSplitOv = this.FindControl<global::Avalonia.Controls.Border>("panSplitOverlay");
        if (pSplitOv != null && pSplitOv.IsVisible)
        {
            pSplitOv.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { pSplitOv.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        }
        var countriesPopup = this.FindControl<global::Avalonia.Controls.Primitives.Popup>("CountriesPopup");
        var languagePopup = this.FindControl<global::Avalonia.Controls.Primitives.Popup>("LanguagePopup");
        if ((countriesPopup != null && countriesPopup.IsOpen) || (languagePopup != null && languagePopup.IsOpen))
        {
            _ = ClosePopupAnimatedAsync();
        }
        var panExpertOverlay = this.FindControl<global::Avalonia.Controls.Border>("panExpertOverlay");
        if (panExpertOverlay != null && panExpertOverlay.IsVisible)
        {
            panExpertOverlay.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panExpertOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
        var panAboutOverlay = this.FindControl<global::Avalonia.Controls.Border>("panAboutOverlay");
        if (panAboutOverlay != null && panAboutOverlay.IsVisible)
        {
            panAboutOverlay.Classes.Remove("popupOpen"); global::Avalonia.Threading.DispatcherTimer.RunOnce(() => { panAboutOverlay.IsVisible = false; }, TimeSpan.FromMilliseconds(200));
        }
    }

    private void btnSettingsClose_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panSettingsOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSettingsOverlay");
        if (panSettingsOverlay != null)
        {
            CloseAllOverlays();
        }
    }

    private void btnXrayExitNodeToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {

        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togXrayExitNode")
                return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNodeToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnXrayExitNodeToggle");
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNode");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoXrayExitNodeExpander");
        var txt = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayJson");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togXrayExitNode");
        
        if (pan != null && ico != null && txt != null && tog != null)
        {
            if (pan.MaxHeight == 0)
            {
                txt.Text = _cfg.V2rayChainJson;
                tog.IsChecked = _cfg.EnableV2rayChain;
                
                pan.MaxHeight = 350;
                pan.Opacity = 1;
                
                var transform = new global::Avalonia.Media.RotateTransform(180);
                ico.RenderTransform = transform;
                
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
            }
            else
            {
                pan.MaxHeight = 0;
                pan.Opacity = 0;
                
                var transform = new global::Avalonia.Media.RotateTransform(0);
                ico.RenderTransform = transform;
                
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            }
        }
    }

    private void btnXrayCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNode");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoXrayExitNodeExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNodeToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnXrayExitNodeToggle");
        if (pan != null && ico != null)
        {
            pan.MaxHeight = 0;
            pan.Opacity = 0;
            var transform = new global::Avalonia.Media.RotateTransform(0);
            ico.RenderTransform = transform;
            if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
        }
    }

    private async void btnXraySave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txt = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayJson");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togXrayExitNode");
        
        if (txt != null && tog != null)
        {
            var text = txt.Text ?? "";
            bool enable = tog.IsChecked ?? false;
            
            if (string.IsNullOrWhiteSpace(text))
            {
                _cfg.V2rayChainJson = "";
                _cfg.EnableV2rayChain = enable;
                ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
                
                btnXrayCancel_Click(sender, e);
                return;
            }
            
            try
            {
                var parsed = Newtonsoft.Json.Linq.JObject.Parse(text);
                Newtonsoft.Json.Linq.JToken? testNode = parsed["outbounds"] is Newtonsoft.Json.Linq.JArray arr ? arr.FirstOrDefault() : parsed;
                if (testNode?["protocol"] == null)
                    throw new Exception("Missing 'protocol' field.");
                
                var streamSettings = testNode["streamSettings"];
                if (streamSettings != null)
                {
                    if (streamSettings["security"]?.ToString()?.ToLowerInvariant() == "reality")
                    {
                        ShowToast(CrimsonOnion.Localization.AppStrings.ToastRealityNotSupported);
                        return;
                    }
                    
                    var net = streamSettings["network"]?.ToString()?.ToLowerInvariant();
                    if (net == "kcp" || net == "quic")
                    {
                        ShowToast(CrimsonOnion.Localization.AppStrings.ToastKcpQuicNotSupported);
                        return;
                    }
                }
                
                var settings = testNode["settings"];
                if (settings != null)
                {
                    var ports = settings.SelectTokens("..port").ToList();
                    foreach (var portToken in ports)
                    {
                        if (int.TryParse(portToken.ToString(), out int port))
                        {
                            if (port != 80 && port != 443)
                            {
                                ShowToast(CrimsonOnion.Localization.AppStrings.ToastPortsSupported);
                                return;
                            }
                        }
                    }
                }
                
                string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xray_test.json");
                try
                {
                    System.IO.File.WriteAllText(tempFile, text);
                    
                    string xrayExe = System.IO.Path.Combine(_cfg.BaseDir, "Data", "xray", "xray.exe");
                    if (System.IO.File.Exists(xrayExe))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = xrayExe,
                            Arguments = $"-test -config \"{tempFile}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        
                        using (var proc = System.Diagnostics.Process.Start(psi))
                        {
                            if (proc != null)
                            {
                                var errTask = proc.StandardError.ReadToEndAsync();
                                await proc.WaitForExitAsync();
                                if (proc.ExitCode != 0)
                                {
                                    string err = await errTask;
                                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastXrayRejected + err.Substring(0, System.Math.Min(err.Length, 100)));
                                    return;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    try { if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile); } catch { }
                }

                _cfg.V2rayChainJson = text.Trim();
                _cfg.EnableV2rayChain = true;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    tog.IsChecked = true;
                });
                
                ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
                if (_state.IsEngineRunning) SmartRestartXray();
                
                btnXrayCancel_Click(sender, e);
            }
            catch (Exception)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastInvalidJson);
            }
        }
    }


    private void txtXrayJson_TextChanged(object? sender, global::Avalonia.Controls.TextChangedEventArgs e)
    {
        var txt = sender as global::Avalonia.Controls.TextBox;
        if (txt == null || string.IsNullOrWhiteSpace(txt.Text)) return;

        string text = txt.Text.Trim();
        
        if (text.StartsWith("vless://") || text.StartsWith("vmess://") || text.StartsWith("trojan://") || text.StartsWith("ss://"))
        {
            if (text.Contains("security=reality", StringComparison.OrdinalIgnoreCase))
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastRealityNotSupported);
                return;
            }
            if (text.Contains("type=kcp", StringComparison.OrdinalIgnoreCase) || text.Contains("net=kcp", StringComparison.OrdinalIgnoreCase) || text.Contains("type=quic", StringComparison.OrdinalIgnoreCase) || text.Contains("net=quic", StringComparison.OrdinalIgnoreCase))
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastKcpQuicNotSupported);
                return;
            }
        }

        if (CrimsonOnion.Services.XrayLinkParser.TryParseLink(text, out string json))
        {
            txt.Text = json;
            ShowToast(CrimsonOnion.Localization.AppStrings.ToastLinkConverted, success: true);
        }
    }

    private async void btnXrayImport_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = global::Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Xray JSON File",
                AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }, new global::Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } } }
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                var path = file.Path.LocalPath;
                if (System.IO.File.Exists(path))
                {
                    var txt = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayJson");
                    if (txt != null)
                        txt.Text = System.IO.File.ReadAllText(path);
                }
            }
        }
        catch
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.ToastFailedImport);
        }
    }

    private async void SettingTog_CheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;

        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        bool val = tog.IsChecked ?? false;

        switch (tog.Name)
        {
            case "btnBootTog":
                try {
                    string exe = System.Environment.ProcessPath ?? "";
                    await CrimsonOnion.Services.ProcessService.UpdateBootScheduledTask(val, exe);
                    _cfg.LaunchOnBoot = val;
                } catch (System.Exception ex) {
                    _cfg.LaunchOnBoot = false;
                    tog.IsChecked = false;
                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastTaskFailed + ex.Message);
                }
                break;
            case "btnAutoTog":
                _cfg.AutoStart = val;
                break;
            case "btnStartMinTog":
                _cfg.StartMinimized = val;
                break;
            case "btnTrayTog":
                _cfg.MinimizeToTray = val;
                break;
            case "btnAdBlockTog":
                _cfg.EnableAdBlock = val;
                if (_state.IsEngineRunning) SmartRestartXray();
                break;
            case "btnLanTog":
                _cfg.AllowLanConnections = val;
                UpdateLanPortUI();
                SmartRestartXray();
                break;
            case "btnDebugTog":
                _cfg.DebugMode = val;
                break;
        }

        RequestConfigSave();
    }

    private void Shortcut_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var btn = sender as global::Avalonia.Controls.Button;
        if (btn == null) return;

        try
        {
            Type? wshType = Type.GetTypeFromProgID("WScript.Shell");
            if (wshType == null) return;
            var ws = (dynamic)Activator.CreateInstance(wshType)!;

            string destPath = "";
            if (btn.Name == "btnDesktopShortcut")
            {
                destPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "CrimsonOnion.lnk");
            }
            else if (btn.Name == "btnStartMenuShortcut")
            {
                string programsPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.StartMenu), "Programs");
                if (!System.IO.Directory.Exists(programsPath)) System.IO.Directory.CreateDirectory(programsPath);
                destPath = System.IO.Path.Combine(programsPath, "CrimsonOnion.lnk");
            }

            dynamic sc = ws.CreateShortcut(destPath);
            sc.TargetPath = System.Environment.ProcessPath ?? "";
            sc.WorkingDirectory = _cfg.BaseDir;
            sc.Save();

            ShowToast(CrimsonOnion.Localization.AppStrings.ToastShortcutCreated, success: true);
        }
        catch
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.ToastShortcutFailed);
        }
    }

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
        private void togXrayExitNode_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            if (tog.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_cfg.V2rayChainJson))
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => tog.IsChecked = false);
                    
                    var panXrayExitNode = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNode");
                    var icoXrayExitNodeExpander = this.FindControl<global::Avalonia.Controls.PathIcon>("icoXrayExitNodeExpander");
                    
                    if (panXrayExitNode != null && panXrayExitNode.MaxHeight == 0)
                    {
                        panXrayExitNode.MaxHeight = 500;
                        panXrayExitNode.Opacity = 1;
                        if (icoXrayExitNodeExpander != null)
                            icoXrayExitNodeExpander.RenderTransform = new global::Avalonia.Media.RotateTransform(180);
                        
                        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNodeToggle");
                        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnXrayExitNodeToggle");
                        if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                        if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                    }
                    return;
                }
                else if (!_cfg.EnableV2rayChain)
                {
                    _cfg.EnableV2rayChain = true;
                    RequestConfigSave();
                    if (_state.IsEngineRunning) SmartRestartXray();
                }
            }
            else
            {
                if (_cfg.EnableV2rayChain)
                {
                    _cfg.EnableV2rayChain = false;
                    RequestConfigSave();
                    if (_state.IsEngineRunning) SmartRestartXray();
                }
            }
        }
    }

    private void btnOutboundToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {

        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togOutboundProxy")
                return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panOutboundToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnOutboundToggle");
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");
        
        var txtAddr = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundAddr");
        var txtPort = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPort");
        var cmbType = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbOutboundType");
        var togAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundAuth");
        var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundUser");
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPass");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundProxy");
        var panAuth = this.FindControl<global::Avalonia.Controls.Border>("panOutboundAuth");

        if (pan != null && ico != null && txtAddr != null && tog != null && txtPort != null)
        {
            if (pan.MaxHeight == 0)
            {
                txtAddr.Text = _cfg.OutboundProxyAddress;
                txtPort.Text = _cfg.OutboundProxyPort;
                SelectComboItem(cmbType, string.IsNullOrEmpty(_cfg.OutboundProxyType) ? "SOCKS5" : _cfg.OutboundProxyType);
                if (togAuth != null) togAuth.IsChecked = _cfg.EnableOutboundAuth;
                if (txtUser != null) txtUser.Text = _cfg.OutboundProxyUser;
                if (txtPass != null) txtPass.Text = _cfg.OutboundProxyPass;
                tog.IsChecked = _cfg.EnableOutboundProxy;

                if (panAuth != null)
                {
                    if (_cfg.EnableOutboundAuth)
                    {
                        panAuth.MaxHeight = 150;
                        panAuth.Opacity = 1;
                    }
                    else
                    {
                        panAuth.MaxHeight = 0;
                        panAuth.Opacity = 0;
                    }
                }
                
                pan.MaxHeight = 350;
                pan.Opacity = 1;
                
                var transform = new global::Avalonia.Media.RotateTransform(180);
                ico.RenderTransform = transform;
                
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
            }
            else
            {
                pan.MaxHeight = 0;
                pan.Opacity = 0;
                var transform = new global::Avalonia.Media.RotateTransform(0);
                ico.RenderTransform = transform;
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            }
        }
    }

    private void SelectComboItem(global::Avalonia.Controls.ComboBox? combo, string content)
    {
        if (combo == null) return;
        foreach (var itemObj in combo.Items)
        {
            if (itemObj is global::Avalonia.Controls.ComboBoxItem item)
            {
                if ((string?)item.Content == content) { combo.SelectedItem = item; return; }
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void btnOutboundCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panOutboundToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnOutboundToggle");
        if (pan != null && ico != null)
        {
            pan.MaxHeight = 0;
            pan.Opacity = 0;
            var transform = new global::Avalonia.Media.RotateTransform(0);
            ico.RenderTransform = transform;
            if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
        }
    }

    private void btnOutboundSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtAddr = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundAddr");
        var txtPort = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPort");
        var cmbType = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbOutboundType");
        var togAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundAuth");
        var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundUser");
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPass");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundProxy");

        if (txtAddr != null && tog != null)
        {
            var addr = txtAddr.Text?.Trim() ?? "";
            var port = txtPort?.Text?.Trim() ?? "";
            bool enable = tog.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(addr))
            {
                _cfg.OutboundProxyAddress = "";
                _cfg.OutboundProxyPort = "";
                _cfg.EnableOutboundProxy = enable;
                RequestConfigSave();
                btnOutboundCancel_Click(sender, e);
                if (_state.IsEngineRunning) ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                return;
            }

            _cfg.OutboundProxyAddress = addr;
            _cfg.OutboundProxyPort = port;
            _cfg.OutboundProxyType = cmbType?.SelectedItem is global::Avalonia.Controls.ComboBoxItem pt ? (string?)pt.Content ?? "SOCKS5" : "SOCKS5";
            _cfg.EnableOutboundAuth = togAuth?.IsChecked ?? false;
            _cfg.OutboundProxyUser = txtUser?.Text?.Trim() ?? "";
            _cfg.OutboundProxyPass = txtPass?.Text?.Trim() ?? "";
            
            _cfg.EnableOutboundProxy = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                tog.IsChecked = true;
            });
            
            RequestConfigSave();
            btnOutboundCancel_Click(sender, e);
            if (_state.IsEngineRunning) ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
        }
    }

    private void togOutboundAuth_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        var panAuth = this.FindControl<global::Avalonia.Controls.Border>("panOutboundAuth");
        if (tog != null && panAuth != null)
        {
            if (tog.IsChecked == true)
            {
                panAuth.MaxHeight = 150;
                panAuth.Opacity = 1;
            }
            else
            {
                panAuth.MaxHeight = 0;
                panAuth.Opacity = 0;
            }
        }
    }

    private void togDirectUDP_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            _cfg.EnableDirectUDP = tog.IsChecked == true;
            RequestConfigSave();
            if (_state.IsEngineRunning)
            {
                if (_cfg.LastXrayMode == "Proxy Mode" || _cfg.LastXrayMode == "Clear Proxy")
                    SmartRestartXray();
                else
                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
            }
        }
    }

    private void togOutboundProxy_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            if (tog.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_cfg.OutboundProxyAddress))
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        tog.IsChecked = false;
                    });
                    
                    var pan = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
                    var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");
                    
                    if (pan != null && pan.MaxHeight == 0)
                    {
                        pan.MaxHeight = 350;
                        pan.Opacity = 1;
                        if (ico != null)
                            ico.RenderTransform = new global::Avalonia.Media.RotateTransform(180);
                        
                        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panOutboundToggle");
                        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnOutboundToggle");
                        if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                        if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                    }
                    return;
                }
                else if (!_cfg.EnableOutboundProxy)
                {
                    _cfg.EnableOutboundProxy = true;
                    RequestConfigSave();
                    if (_state.IsEngineRunning) ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            else
            {
                if (_cfg.EnableOutboundProxy)
                {
                    _cfg.EnableOutboundProxy = false;
                    RequestConfigSave();
                    if (_state.IsEngineRunning) ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            UpdateAdapterBindingMutualExclusivity();
        }
    }

    private void btnAdapterBindingToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togAdapterBinding") return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var pan = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBinding");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoAdapterBindingExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBindingToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnAdapterBindingToggle");
        if (pan != null)
        {
            if (pan.MaxHeight == 0)
            {
                pan.MaxHeight = 200;
                pan.Opacity = 1;
                if (ico != null) ico.RenderTransform = new global::Avalonia.Media.RotateTransform(180);
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                
                var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbAdapters");
                if (cmb != null && cmb.Items.Count == 0)
                {
                    btnScanAdapters_Click(null, null);
                }
            }
            else
            {
                pan.MaxHeight = 0;
                pan.Opacity = 0;
                if (ico != null) ico.RenderTransform = new global::Avalonia.Media.RotateTransform(0);
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            }
        }
    }

    private void togAdapterBinding_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            if (tog.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_cfg.SelectedAdapterIp))
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                    var pan = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBinding");
                    if (pan != null && pan.MaxHeight == 0)
                    {
                        pan.MaxHeight = 200;
                        pan.Opacity = 1;
                        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoAdapterBindingExpander");
                        if (ico != null) ico.RenderTransform = new global::Avalonia.Media.RotateTransform(180);
                        
                        var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbAdapters");
                        if (cmb != null && cmb.Items.Count == 0)
                        {
                            btnScanAdapters_Click(null, null);
                        }
                    }
                    return;
                }
                else if (!_cfg.EnableAdapterBinding)
                {
                    _cfg.EnableAdapterBinding = true;
                    RequestConfigSave();
                    if (_state.IsEngineRunning) ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            else
            {
                if (_cfg.EnableAdapterBinding)
                {
                    _cfg.EnableAdapterBinding = false;
                    RequestConfigSave();
                    if (_state.IsEngineRunning) ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            UpdateAdapterBindingMutualExclusivity();
        }
    }

    private void cmbAdapters_SelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
    {
        var cmb = sender as global::Avalonia.Controls.ComboBox;
        if (cmb != null && cmb.SelectedItem is string selectedText && !string.IsNullOrWhiteSpace(selectedText))
        {
            var parts = selectedText.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                _cfg.SelectedAdapterName = parts[0];
                _cfg.SelectedAdapterIp = parts[1];
                RequestConfigSave();
                if (_state.IsEngineRunning)
                {
                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
        }
    }

    private void btnScanAdapters_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs? e = null)
    {
        var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbAdapters");
        if (cmb == null) return;
        
        cmb.Items.Clear();
        var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
        foreach (var adapter in adapters)
        {
            if (adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                adapter.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
            {
                var properties = adapter.GetIPProperties();
                var ipv4 = properties.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipv4 != null && !string.IsNullOrWhiteSpace(ipv4.Address.ToString()))
                {
                    cmb.Items.Add($"{adapter.Name} - {ipv4.Address}");
                }
            }
        }
        
        if (!string.IsNullOrWhiteSpace(_cfg.SelectedAdapterName) && !string.IsNullOrWhiteSpace(_cfg.SelectedAdapterIp))
        {
            var toSelect = $"{_cfg.SelectedAdapterName} - {_cfg.SelectedAdapterIp}";
            var itemsList = cmb.Items.Cast<string>().ToList();
            var index = itemsList.IndexOf(toSelect);
            if (index >= 0)
            {
                cmb.SelectedIndex = index;
            }
            else
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian ? "آداپتور شبکه قبلی شما دیگر در دسترس نیست." : "Your previously selected network adapter is no longer available.");
                _cfg.SelectedAdapterName = "";
                _cfg.SelectedAdapterIp = "";
                RequestConfigSave();
                
                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
            }
        }
        else if (cmb.Items.Count > 0)
        {
            cmb.SelectedIndex = 0;
        }
    }

    // ─── System DNS state 
    private string?   _savedDnsAdapterName;
    private string[]? _savedDnsServers;

    // ─── DNS Settings panel toggle (expand/collapse) 
    private void btnDnsToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togDnsSettings" || src.Name == "togSysDns")
                return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panDnsToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnDnsToggle");
        var pan       = this.FindControl<global::Avalonia.Controls.Border>("panDnsSettings");
        var ico       = this.FindControl<global::Avalonia.Controls.PathIcon>("icoDnsExpander");
        var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");

        if (pan != null && ico != null && cmbDohUrl != null)
        {
            if (pan.MaxHeight == 0)
            {
                cmbDohUrl.Text = _cfg.UpstreamDohUrl;

                var txtPrimary   = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
                var txtSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
                if (txtPrimary   != null) txtPrimary.Text   = _cfg.SystemDnsPrimary;
                if (txtSecondary != null) txtSecondary.Text = _cfg.SystemDnsSecondary;

                pan.MaxHeight = 340;
                pan.Opacity   = 1;

                var transform = new global::Avalonia.Media.RotateTransform(180);
                ico.RenderTransform = transform;

                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
            }
            else
            {
                CloseDnsPanel();
            }
        }
    }

    private void CloseDnsPanel()
    {
        var pan       = this.FindControl<global::Avalonia.Controls.Border>("panDnsSettings");
        var ico       = this.FindControl<global::Avalonia.Controls.PathIcon>("icoDnsExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panDnsToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnDnsToggle");
        if (pan != null && ico != null)
        {
            pan.MaxHeight = 0;
            pan.Opacity   = 0;
            var transform = new global::Avalonia.Media.RotateTransform(0);
            ico.RenderTransform = transform;
            if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
        }
    }
    // ─── DoH URL inline SAVE button 
    private void btnDohSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");
        var tog       = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togDnsSettings");

        if (cmbDohUrl != null && tog != null)
        {
            var url = cmbDohUrl.Text?.Trim() ?? "";
            _cfg.UpstreamDohUrl    = url;
            _cfg.EnableUpstreamDoh = true;

            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                tog.IsChecked = true;
            });

            RequestConfigSave();
            if (_state.IsEngineRunning) SmartRestartXray();
        }
    }

    // ─── DoH toggle 
    private void togDnsSettings_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        if (tog.IsChecked == true)
        {
            var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");
            var liveUrl   = cmbDohUrl?.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(liveUrl))
                _cfg.UpstreamDohUrl = liveUrl;

            if (string.IsNullOrWhiteSpace(_cfg.UpstreamDohUrl))
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                return;
            }

            _cfg.EnableUpstreamDoh = true;
            RequestConfigSave();
            if (_state.IsEngineRunning) SmartRestartXray();
        }
        else
        {
            if (_cfg.EnableUpstreamDoh)
            {
                _cfg.EnableUpstreamDoh = false;
                RequestConfigSave();
                if (_state.IsEngineRunning) SmartRestartXray();
            }
        }
    }

    // ─── System DNS SAVE button 
    private void btnSysDnsSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtPrimary   = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
        var txtSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
        var tog          = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togSysDns");

        var primary   = txtPrimary?.Text?.Trim()   ?? "";
        var secondary = txtSecondary?.Text?.Trim() ?? "";

        if (!DnsService.IsValidIpv4(primary))
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                ? "لطفاً یک آدرس IPv4 معتبر برای DNS اول وارد کنید."
                : "Please enter a valid IPv4 address for the primary DNS.");
            return;
        }
        if (!string.IsNullOrWhiteSpace(secondary) && !DnsService.IsValidIpv4(secondary))
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                ? "لطفاً یک آدرس IPv4 معتبر برای DNS دوم وارد کنید."
                : "Please enter a valid IPv4 address for the secondary DNS.");
            return;
        }

        _cfg.SystemDnsPrimary   = primary;
        _cfg.SystemDnsSecondary = secondary;
        _cfg.EnableSystemDns    = true;

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            if (tog != null) tog.IsChecked = true;
        });

        RequestConfigSave();
        if (_state.IsEngineRunning)
            ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectDns);
    }

    // ─── System DNS toggle 
    private void togSysDns_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        if (tog.IsChecked == true)
        {
            var txtPrimary   = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
            var txtSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
            var livePrimary   = txtPrimary?.Text?.Trim()   ?? "";
            var liveSecondary = txtSecondary?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(livePrimary))
            {
                if (!DnsService.IsValidIpv4(livePrimary))
                {
                    ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                        ? "لطفاً یک آدرس IPv4 معتبر برای DNS اول وارد کنید."
                        : "Please enter a valid IPv4 address for the primary DNS.");
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                    return;
                }
                if (!string.IsNullOrWhiteSpace(liveSecondary) && !DnsService.IsValidIpv4(liveSecondary))
                {
                    ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                        ? "لطفاً یک آدرس IPv4 معتبر برای DNS دوم وارد کنید."
                        : "Please enter a valid IPv4 address for the secondary DNS.");
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                    return;
                }
                _cfg.SystemDnsPrimary   = livePrimary;
                _cfg.SystemDnsSecondary = liveSecondary;
            }

            if (string.IsNullOrWhiteSpace(_cfg.SystemDnsPrimary))
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                return;
            }

            _cfg.EnableSystemDns = true;
            RequestConfigSave();
            if (_state.IsEngineRunning)
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectDns);
        }
        else
        {
            if (_cfg.EnableSystemDns)
            {
                _cfg.EnableSystemDns = false;
                RequestConfigSave();
                if (_state.IsEngineRunning)
                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectDns);
            }
        }
    }

    // ─── Allow LAN expandable panel 
    private void btnLanToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "btnLanTog" || src.Name == "togLanAuth") return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panLanToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnLanToggle");
        var pan       = this.FindControl<global::Avalonia.Controls.Border>("panLanSettings");
        var ico       = this.FindControl<global::Avalonia.Controls.PathIcon>("icoLanExpander");

        if (pan == null || ico == null) return;

        if (pan.MaxHeight == 0)
        {
            var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanUser");
            var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
            var tog     = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togLanAuth");
            if (txtUser != null) txtUser.Text = _cfg.LanAuthUsername;
            if (txtPass != null) txtPass.Text = _cfg.LanAuthPassword;
            if (tog     != null) tog.IsChecked = _cfg.EnableLanAuth;

            pan.MaxHeight = 160;
            pan.Opacity   = 1;
            ico.RenderTransform = new global::Avalonia.Media.RotateTransform(180);
            if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
            if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
        }
        else
        {
            pan.MaxHeight = 0;
            pan.Opacity   = 0;
            ico.RenderTransform = new global::Avalonia.Media.RotateTransform(0);
            if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
        }
    }

    // ─── LAN auth toggle 
    private void togLanAuth_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        if (tog.IsChecked == true)
        {
            var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanUser");
            var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
            var liveUser = txtUser?.Text?.Trim() ?? "";
            var livePass = txtPass?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(liveUser))
            {
                _cfg.LanAuthUsername = liveUser;
                _cfg.LanAuthPassword = livePass;
            }

            if (string.IsNullOrWhiteSpace(_cfg.LanAuthUsername))
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                return;
            }

            _cfg.EnableLanAuth = true;
            RequestConfigSave();

            if (_state.IsEngineRunning)
            {
                if (_pollMode == "VPN Mode")
                    ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                        ? "\u0628\u0631\u0627\u06cc \u0627\u0639\u0645\u0627\u0644 \u062a\u063a\u06cc\u06cc\u0631\u0627\u062a \u062f\u0648\u0628\u0627\u0631\u0647 \u0645\u062a\u0635\u0644 \u0634\u0648\u06cc\u062f."
                        : "Reconnect to apply the changes.");
                else
                    SmartRestartXray();
            }
        }
        else
        {
            if (_cfg.EnableLanAuth)
            {
                _cfg.EnableLanAuth = false;
                RequestConfigSave();

                if (_state.IsEngineRunning)
                {
                    if (_pollMode == "VPN Mode")
                        ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                            ? "\u0628\u0631\u0627\u06cc \u0627\u0639\u0645\u0627\u0644 \u062a\u063a\u06cc\u06cc\u0631\u0627\u062a \u062f\u0648\u0628\u0627\u0631\u0647 \u0645\u062a\u0635\u0644 \u0634\u0648\u06cc\u062f."
                        : "Reconnect to apply the changes.");
                    else
                        SmartRestartXray();
                }
            }
        }
    }

    // ─── LAN auth SAVE button 
    private void btnLanAuthSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanUser");
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
        var tog     = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togLanAuth");

        var user = txtUser?.Text?.Trim() ?? "";
        var pass = txtPass?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(user))
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                ? "\u0644\u0637\u0641\u0627\u064b \u0646\u0627\u0645 \u06a9\u0627\u0631\u0628\u0631\u06cc \u0631\u0627 \u0648\u0627\u0631\u062f \u06a9\u0646\u06cc\u062f."
                : "Please enter a username.");
            return;
        }

        _cfg.LanAuthUsername = user;
        _cfg.LanAuthPassword = pass;
        _cfg.EnableLanAuth   = true;

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            if (tog != null) tog.IsChecked = true;
        });

        RequestConfigSave();

        if (_state.IsEngineRunning)
        {
            if (_pollMode == "VPN Mode")
                ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                    ? "\u0628\u0631\u0627\u06cc \u0627\u0639\u0645\u0627\u0644 \u062a\u063a\u06cc\u06cc\u0631\u0627\u062a \u062f\u0648\u0628\u0627\u0631\u0647 \u0645\u062a\u0635\u0644 \u0634\u0648\u06cc\u062f."
                    : "Reconnect to apply the changes.");
            else
                SmartRestartXray();
        }
        else
        {
            ShowToast(CrimsonOnion.Localization.AppStrings.IsPersian
                ? "\u0627\u0637\u0644\u0627\u0639\u0627\u062a \u0648\u0631\u0648\u062f \u0630\u062e\u06cc\u0631\u0647 \u0634\u062f."
                : "Credentials saved.", success: true);
        }
    }

    // ─── LAN password show/hide eye 
    private bool _lanPassVisible = false;
    private void btnLanPassEye_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
        var ico     = this.FindControl<global::Avalonia.Controls.PathIcon>("icoLanPassEye");
        if (txtPass == null) return;

        _lanPassVisible = !_lanPassVisible;
        txtPass.PasswordChar = _lanPassVisible ? '\0' : '\u2022';

        if (ico != null)
            ico.Data = _lanPassVisible
                ? global::Avalonia.Media.Geometry.Parse("M12 7c2.76 0 5 2.24 5 5 0 .65-.13 1.26-.36 1.83l2.92 2.92c1.51-1.26 2.7-2.89 3.43-4.75-1.73-4.39-6-7.5-11-7.5-1.4 0-2.74.25-3.98.7l2.16 2.16C10.74 7.13 11.35 7 12 7zM2 4.27l2.28 2.28.46.46C3.08 8.3 1.78 10.02 1 12c1.73 4.39 6 7.5 11 7.5 1.55 0 3.03-.3 4.38-.84l.42.42L19.73 22 21 20.73 3.27 3 2 4.27zM7.53 9.8l1.55 1.55c-.05.21-.08.43-.08.65 0 1.66 1.34 3 3 3 .22 0 .44-.03.65-.08l1.55 1.55c-.67.33-1.41.53-2.2.53-2.76 0-5-2.24-5-5 0-.79.2-1.53.53-2.2zm4.31-.78l3.15 3.15.02-.16c0-1.66-1.34-3-3-3l-.17.01z")
                : global::Avalonia.Media.Geometry.Parse("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");
    }

    // ─── Apply system DNS at connect time 
    private void ApplySystemDns()
    {
        if (!_cfg.EnableSystemDns) return;
        if (string.IsNullOrWhiteSpace(_cfg.SystemDnsPrimary)) return;

        try
        {
            System.Net.NetworkInformation.NetworkInterface? nic = null;
            if (_cfg.EnableAdapterBinding && !string.IsNullOrWhiteSpace(_cfg.SelectedAdapterName))
            {
                nic = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(a => a.Name == _cfg.SelectedAdapterName && a.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);
            }
            
            if (nic == null)
            {
                nic = DnsService.GetMainPhysicalAdapter();
            }

            if (nic == null)
            {
                System.Diagnostics.Debug.WriteLine("[DnsService] No valid adapter found for DNS.");
                return;
            }
            _savedDnsAdapterName = nic.Name;
            _savedDnsServers     = DnsService.GetCurrentDns(nic);

            DnsService.SetDns(nic.Name, _cfg.SystemDnsPrimary, _cfg.SystemDnsSecondary);
            System.Diagnostics.Debug.WriteLine($"[DnsService] Applied DNS {_cfg.SystemDnsPrimary}/{_cfg.SystemDnsSecondary} to {nic.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DnsService] ApplySystemDns error: {ex.Message}");
        }
    }

    // ─── Restore system DNS at disconnect / app close 
    private void RestoreSystemDns()
    {
        if (_savedDnsAdapterName == null) return;

        try
        {
            DnsService.RestoreDns(_savedDnsAdapterName, _savedDnsServers ?? Array.Empty<string>());
            System.Diagnostics.Debug.WriteLine($"[DnsService] Restored DNS on {_savedDnsAdapterName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DnsService] RestoreSystemDns error: {ex.Message}");
        }
        finally
        {
            _savedDnsAdapterName = null;
            _savedDnsServers     = null;
        }
    }
}
