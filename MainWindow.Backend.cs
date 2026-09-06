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
    private long _lastEngineCountToastTick = 0;

    private System.Threading.CancellationTokenSource? _statsCts;
    private System.Threading.CancellationTokenSource? _pingCts;


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
    private global::Avalonia.Threading.DispatcherTimer? _graphTimer;
    private global::Avalonia.Media.TranslateTransform? _graphTranslate;
    private double _graphTargetX;
    private double _graphStepX;
    private int _isFetchingStatsInt = 0; 
    private System.Collections.Generic.Queue<double> _upHistory = new();
    private System.Collections.Generic.Queue<double> _dnHistory = new();
    private double _upSum = 0;
    private double _dnSum = 0;
    private long _lastUpBytes = 0;
    private long _lastDnBytes = 0;
    private DateTime _lastPollTime = DateTime.MinValue;


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
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }
    }

    // ── Process management — delegated to VpnEngineService ──────────────────

    private void KillPidRef(ref int? pidRef)
        => CrimsonOnion.Services.VpnEngineService.KillPidRef(ref pidRef);

    private void KillPid(int? pid)
        => CrimsonOnion.Services.VpnEngineService.KillPid(pid);

    private void KillManagedProcesses(params string[] names)
        => CrimsonOnion.Services.VpnEngineService.KillManagedProcesses(_cfg.BaseDir, names);

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
                    lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.Disabled}";
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

        var padded = torIdx.ToString().PadLeft(2, '0');

        int uiSelCount = _activeTorEngines;

        if (!_state.IsEngineRunning)
        {
            if (torIdx > uiSelCount)
            {
                lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.Disabled}";
            }
            else
            {
                lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusOffline}";
            }
            lbl.Foreground = BrGray;
            lbl.Opacity = 0.5;
            return;
        }

        if (torIdx > uiSelCount && torIdx > _pollSelCount)
        {
            lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.Disabled}";
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




    internal async Task OnEngineCountChanged(int newCount)
    {
        RequestConfigSave();

        if (_state.IsConnected)
        {
            long now = Environment.TickCount64;
            if (now - _lastEngineCountToastTick > 3000)
            {
                _lastEngineCountToastTick = now;
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
            }
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
                lock (_staggerQueue) { _staggerQueue.Remove(i); }
                UpdateTorLabel(i);
            }
        }
        else if (newCount > curCount)
        {
            _pollSelCount = newCount;

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
                if (isBootstrapping) { lock (_staggerQueue) { _staggerQueue.Add(i); } }
            }

            bool hasStaggerItems = false;
            lock (_staggerQueue) { hasStaggerItems = _staggerQueue.Count > 0; }
            if (isBootstrapping && hasStaggerItems)
            {
                if (_staggerTimer?.IsEnabled != true)
                {
                    _staggerTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    _staggerTimer.Start();
                    
                    _ = Task.Run(async () =>
                    {
                        while (true)
                        {
                            await Task.Delay(1500);
                            if (_state.AbortBoot || !_state.IsEngineRunning) break;
                            
                            int idx = -1;
                            lock (_staggerQueue)
                            {
                                if (_staggerQueue.Count > 0)
                                {
                                    idx = _staggerQueue[0];
                                    _staggerQueue.RemoveAt(0);
                                }
                            }
                            if (idx == -1) break;
                            await LaunchSingleTorAsync(idx);
                        }
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => _staggerTimer?.Stop());
                    });
                }
            }
        }
    }

    private async Task LaunchSingleTorAsync(int i)
    {
        var torPath   = GetAppPath($@"Data\Tors\Tor{i}");
        var torrcFile = "torrc";

        TryDeleteFile(Path.Combine(torPath, "tor.log"));
        TryDeleteFile(Path.Combine(torPath, "Data", "control_auth_cookie"));

        if (!Directory.Exists(torPath)) Directory.CreateDirectory(torPath);

        var lines = TorrcBuilder.BuildTorrcConfig(torrcFile, _pollSelBridge, _cfg.LastConfig, torPath, _cfg, out var dnsttProxies);
        await File.WriteAllLinesAsync(Path.Combine(torPath, torrcFile), lines);
        
        if (dnsttProxies != null && dnsttProxies.Count > 0 && i == 1)
        {
            Action<string>? dnsttLogger = null;
            if (_cfg.DebugMode)
            {
                dnsttLogger = (line) => 
                {
                    try 
                    {
                        using (var fs = new System.IO.FileStream(GetAppPath("debug.log"), System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite, 4096, false))
                        using (var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
                        {
                            sw.WriteLine($"[dnstt{i}] {line}");
                        }
                    } catch { }
                };
            }
            CrimsonOnion.Services.DnsttManager.StartTunnels(dnsttProxies, GetAppPath(@"Data\TorBin"), _cfg, dnsttLogger);
        }

        if (_cfg.DebugMode)
        {
            int? pid = StartDebugProcess(
                GetAppPath(@"Data\TorBin\tor.exe"),
                $"-f {torrcFile}",
                torPath,
                $"Tor{i}");
            if (pid.HasValue)
            {
                _torPids[i - 1] = pid.Value;

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
        else
        {
        using (var proc = ProcessService.StartProcessDirect(
            GetAppPath(@"Data\TorBin\tor.exe"),
            $"-f {torrcFile}",
            torPath))
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
                    if (!_torControlClients.Contains(controlClient)) return;
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
                    if (!_torControlClients.Contains(controlClient)) return;
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
    }


    private global::Avalonia.Threading.DispatcherTimer? _fillAnimTimer;
    private double _currentFillPct = 0;
    private double _targetFillPct = -1;

    private global::Avalonia.Controls.TextBlock? _fillTxtBg;
    private global::Avalonia.Controls.TextBlock? _fillTxtConnected;
    private global::Avalonia.Media.LinearGradientBrush? _fillBrush;
    private global::Avalonia.Media.GradientStop? _fillStop1;  
    private global::Avalonia.Media.GradientStop? _fillStop2;  
    private global::Avalonia.Media.GradientStop? _fillStop3;  
    private global::Avalonia.Media.GradientStop? _fillStop4;  

    private void EnsureFillResources()
    {
        if (_fillTxtBg == null)
            _fillTxtBg = this.FindControl<global::Avalonia.Controls.TextBlock>("txtConnectBtn");
        if (_fillTxtConnected == null)
            _fillTxtConnected = this.FindControl<global::Avalonia.Controls.TextBlock>("txtConnectedBtn");

        if (_fillBrush == null)
        {
            var orange = global::Avalonia.Media.Color.Parse("#DD6B20");
            _fillStop1 = new global::Avalonia.Media.GradientStop(orange, 0.0);
            _fillStop2 = new global::Avalonia.Media.GradientStop(orange, 0.0);
            _fillStop3 = new global::Avalonia.Media.GradientStop(global::Avalonia.Media.Colors.White, 0.0);
            _fillStop4 = new global::Avalonia.Media.GradientStop(global::Avalonia.Media.Colors.White, 1.0);
            _fillBrush = new global::Avalonia.Media.LinearGradientBrush
            {
                StartPoint = new global::Avalonia.RelativePoint(0, 0, global::Avalonia.RelativeUnit.Relative),
                EndPoint   = new global::Avalonia.RelativePoint(1, 0, global::Avalonia.RelativeUnit.Relative),
                GradientStops = new global::Avalonia.Media.GradientStops
                    { _fillStop1, _fillStop2, _fillStop3, _fillStop4 }
            };
        }
    }

    private void SetConnectButtonProgress(int percent)
    {
        if (percent < 0)
        {
            _targetFillPct = -1;
            _currentFillPct = 0;
            _fillAnimTimer?.Stop();

            if (_fillTxtBg == null)
                _fillTxtBg = this.FindControl<global::Avalonia.Controls.TextBlock>("txtConnectBtn");
            if (_fillTxtConnected == null)
                _fillTxtConnected = this.FindControl<global::Avalonia.Controls.TextBlock>("txtConnectedBtn");

            if (_fillTxtBg != null) { _fillTxtBg.Foreground = BrWhite; _fillTxtBg.Opacity = 1; }
            if (_fillTxtConnected != null) { _fillTxtConnected.Opacity = 0; }
            return;
        }

        if (_fillAnimTimer == null)
        {
            _fillAnimTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _fillAnimTimer.Tick += (s, e) => {
                if (_targetFillPct < 0) return;

                double diff = _targetFillPct - _currentFillPct;
                if (System.Math.Abs(diff) < 0.005) _currentFillPct = _targetFillPct;
                else _currentFillPct += diff * 0.35;

                EnsureFillResources();

                if (_fillTxtBg != null)
                {
                    if (_currentFillPct <= 0.001)
                    {
                        _fillTxtBg.Foreground = BrWhite;
                    }
                    else
                    {
                        _fillStop2!.Offset = _currentFillPct;
                        _fillStop3!.Offset = _currentFillPct;
                        _fillTxtBg.Foreground = _fillBrush;
                    }
                }

                if (_currentFillPct >= 0.999 && _targetFillPct >= 1.0 && _state.IsConnected)
                {
                    if (_fillTxtBg != null && _fillTxtConnected != null)
                    {
                        _fillTxtConnected.Text = CrimsonOnion.Localization.AppStrings.ConnectedBtn;
                        _fillTxtBg.Opacity = 0;
                        _fillTxtConnected.Opacity = 1;
                    }
                    _targetFillPct = -1;
                    _fillAnimTimer.Stop();
                }
            };
        }

        if (!_fillAnimTimer.IsEnabled)
            _fillAnimTimer.Start();

        _targetFillPct = System.Math.Clamp(percent / 100.0, 0.0, 1.0);
    }


    private void StopAllEngines(bool isClosing = false)
    {
        _state.AbortBoot       = true;
        _state.IsEngineRunning = false;
        
        for (int i = 0; i < 8; i++) _state.TorPcts[i] = -1;
        for (int i = 1; i <= 8; i++) UpdateTorLabel(i);

        _bootstrapTimer?.Stop();
        _staggerTimer?.Stop();
        _xrayBootTimer?.Stop();
        lock (_staggerQueue) { _staggerQueue.Clear(); }
        _sessionClockTimer?.Stop();
        if (_statsCts != null) { try { _statsCts.Cancel(); _statsCts.Dispose(); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _statsCts = null; }
        if (_pingCts != null) { try { _pingCts.Cancel(); _pingCts.Dispose(); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _pingCts = null; }
        if (_geoCts != null) { try { _geoCts.Cancel(); _geoCts.Dispose(); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _geoCts = null; } 
        if (_graphTimer != null) { _graphTimer.Stop(); _graphTimer = null; } 
        _logTimer?.Stop(); 
        _logClearTimer?.Stop(); 
        ProxyService.SetSystemProxy(false);
        _ = RestoreSystemDnsAsync();

        foreach (var client in _torControlClients) client.Dispose();
        _torControlClients.Clear();
        
        SetConnectButtonProgress(-1);

        int? xrayDebugPid = _xrayDebugPid; _xrayDebugPid = null;
        int? sbDebugPid = _sbDebugPid; _sbDebugPid = null;
        int? adapterDebugPid = _adapterXrayDebugPid; _adapterXrayDebugPid = null;
        int? xrayPid = _xrayPid; _xrayPid = null;
        int? sbPid = _sbPid; _sbPid = null;
        int? adapterPid = _adapterXrayPid; _adapterXrayPid = null;

        var killTask = Task.Run(() => {
            KillManagedProcesses("tor", "lyrebird");
            KillPid(xrayDebugPid);
            KillPid(sbDebugPid);
            KillPid(adapterDebugPid);
            KillPid(xrayPid);
            KillPid(sbPid);
            KillPid(adapterPid);
            for (int i = 1; i <= 8; i++)
            {
                TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));
                TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\Data\control_auth_cookie"));
            }
            TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
            TryDeleteFile(GetAppPath(@"Data\Xray\error.log"));
            TryDeleteFile(GetAppPath(@"Data\Xray\access.log.tmp"));
        });
        if (isClosing)
        {
            killTask.Wait(3000);
            CrimsonOnion.Services.JobManager.Shutdown();
        }

        CrimsonOnion.Services.SimpleLogger.Log($"[Disconnect] Bridge={_activeBridge}, Mode={_pollMode}, isClosing={isClosing}");

        _state.IsConnected      = false;
        _state.LastTotalBytes   = 0;
        _state.SessionDataBytes = 0;
        _state.SessionStartTime = null;
        _state.SpeedSamples     = _state.SpeedSamples ?? new double[5]; Array.Clear(_state.SpeedSamples, 0, _state.SpeedSamples.Length);

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            _upHistory.Clear();
            _dnHistory.Clear();
            _upSum = 0;
            _dnSum = 0;
            var graphUpload = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphUpload");
        var graphDownload = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphDownload");
        var graphUploadFill = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphUploadFill");
        var graphDownloadFill = this.FindControl<global::Avalonia.Controls.Shapes.Path>("graphDownloadFill");
        if (graphUpload != null) graphUpload.Data = null;
        if (graphDownload != null) graphDownload.Data = null;
        if (graphUploadFill != null) graphUploadFill.Data = null;
        if (graphDownloadFill != null) graphDownloadFill.Data = null;

        var panTimerContent = this.FindControl<global::Avalonia.Controls.StackPanel>("panTimerContent");
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

    private void RefreshPing_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_state.IsConnected && !_state.IsGeoTracing)
        {
            StartGeoPing();
        }
    }


    private void btnConnect_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_state.IsConnected || _state.IsEngineRunning)
        {
            StopAllEngines();
            return;
        }

        if (_cfg.LastXrayMode == "VPN Mode")
        {
            try
            {
                bool tunExists = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Any(ni => (ni.Name.IndexOf("singbox", StringComparison.OrdinalIgnoreCase) >= 0 
                             || ni.Name.IndexOf("wintun", StringComparison.OrdinalIgnoreCase) >= 0
                             || ni.Description.IndexOf("wintun", StringComparison.OrdinalIgnoreCase) >= 0)
                            && ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);

                if (tunExists)
                {
                    bool isFa = CrimsonOnion.Localization.AppStrings.IsPersian;
                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastVpnAdapterInUse);
                    return;
                }
            }
            catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }
        }

        if (!File.Exists(GetAppPath(@"Data\Tors\Tor1\Data\state")))
        {
            string msg = CrimsonOnion.Localization.AppStrings.ToastFirstConnectionLong;
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
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastAdapterNotAvailable);
                return;
            }
        }

        if (_cfg.EnableDirectUDP && !string.IsNullOrWhiteSpace(_cfg.DirectUdpAdapterName) && _cfg.DirectUdpAdapterName != "default")
        {
            var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            bool exists = false;
            foreach (var adapter in adapters)
            {
                if (adapter.Name == _cfg.DirectUdpAdapterName && adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastDirectUdpAdapterNotAvailable);
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
            CrimsonOnion.Services.SimpleLogger.Log(ex);
            ShowToast(string.Format(CrimsonOnion.Localization.AppStrings.ToastEngineStartFailedFormat, ex.Message));
            StopAllEngines();
        }
    }

    private async Task StartEnginesAsyncCore()
    {
        CrimsonOnion.Services.VpnEngineService.ClearSeenLogs();

        _bootstrapTimer?.Stop();
        _staggerTimer?.Stop();
        _xrayBootTimer?.Stop();
        lock (_staggerQueue) { _staggerQueue.Clear(); }

        _state.IsEngineRunning = true;
        _state.AbortBoot = false;

        if (txtConnectBtn != null)
        {
            txtConnectBtn.Text = CrimsonOnion.Localization.AppStrings.BtnConnecting;
            txtConnectBtn.Foreground = BrWhite;
        }
        SetConnectButtonProgress(0);
        UpdateRingAnimation("Connecting");

        await UpdateLanIpAsync();
        await ApplySystemDnsAsync();

        foreach (var client in _torControlClients) client.Dispose();
        _torControlClients.Clear();
        var torPidsSnapshot = _torPids;
        _torPids = new int?[8];
        int? xrayDebugPid = _xrayDebugPid; _xrayDebugPid = null;
        int? sbDebugPid = _sbDebugPid; _sbDebugPid = null;
        int? adapterDebugPid = _adapterXrayDebugPid; _adapterXrayDebugPid = null;
        int? xrayPid = _xrayPid; _xrayPid = null;
        int? sbPid = _sbPid; _sbPid = null;
        int? adapterPid = _adapterXrayPid; _adapterXrayPid = null;

        var killTask = Task.Run(() => {
            foreach (var pid in torPidsSnapshot) KillPid(pid);
            KillManagedProcesses("tor", "lyrebird");
            KillPid(xrayDebugPid);
            KillPid(sbDebugPid);
            KillPid(adapterDebugPid);
            KillPid(xrayPid);
            KillPid(sbPid);
            KillPid(adapterPid);
        });
        await killTask;

        ProxyService.SetSystemProxy(false);

        _state.IsConnected      = false;
        _state.LastTotalBytes   = 0;
        _state.SessionDataBytes = 0;
        _state.SessionStartTime = null;
        Array.Clear(_state.SpeedSamples, 0, _state.SpeedSamples.Length);

        _pollSelCount  = _activeTorEngines;
        _pollSelBridge = _activeBridge;

        for (int i = 0; i < 8; i++) _state.TorPcts[i] = -1;

        for (int i = 1; i <= 8; i++) UpdateTorLabel(i);

        TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
        var txtXrayLogs = this.FindControl<TextBox>("txtXrayLogs");
        if (txtXrayLogs != null) txtXrayLogs.Text = "";
        Interlocked.Exchange(ref _lastXrayLogPos, 0);
        _xrayLogLines.Clear();

        await Task.Delay(800);
        if (_state.AbortBoot) return;

        await StartBackendXrayAsync();

        for (int i = 1; i <= 8; i++)
            TryDeleteFile(GetAppPath($@"Data\Tors\Tor{i}\tor.log"));



        bool isBridged  = _pollSelBridge != "Direct";
        int warningInterval = isBridged ? 300 : 180;
        DateTime startTime = DateTime.Now;
        DateTime[] nextWarningTime = new[] { DateTime.Now.AddSeconds(warningInterval) };

        _bootstrapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _bootstrapTimer.Tick += (s, e) => BootstrapTick("Optimized", nextWarningTime, warningInterval, startTime);
        _bootstrapTimer.Start();

        for (int i = 1; i <= _pollSelCount; i++)
        {
            if (_state.AbortBoot) break;

            var torPath   = GetAppPath($@"Data\Tors\Tor{i}");
            var torrcFile = "torrc";

            if (!File.Exists(GetAppPath(@"Data\TorBin\tor.exe"))) continue;
            if (!Directory.Exists(torPath)) Directory.CreateDirectory(torPath);

            var lines = TorrcBuilder.BuildTorrcConfig(torrcFile, _pollSelBridge, _cfg.LastConfig, torPath, _cfg, out var dnsttProxies);
            await File.WriteAllLinesAsync(Path.Combine(torPath, torrcFile), lines);
            
            var idx = i;

            int? newPid = null;
            await Task.Run(() =>
            {
                if (dnsttProxies != null && dnsttProxies.Count > 0 && idx == 1)
                {
                    Action<string>? dnsttLogger = null;
                    if (_cfg.DebugMode)
                    {
                        dnsttLogger = (line) => 
                        {
                            try 
                            {
                                using (var fs = new System.IO.FileStream(GetAppPath("debug.log"), System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite, 4096, false))
                                using (var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
                                {
                                    sw.WriteLine($"[dnstt{idx}] {line}");
                                }
                            } catch { }
                        };
                    }
                    CrimsonOnion.Services.DnsttManager.StartTunnels(dnsttProxies, GetAppPath(@"Data\TorBin"), _cfg, dnsttLogger);
                }

                TryDeleteFile(Path.Combine(torPath, "Data", "control_auth_cookie"));

                if (_cfg.DebugMode)
                {
                    newPid = StartDebugProcess(
                        GetAppPath(@"Data\TorBin\tor.exe"),
                        $"-f {torrcFile}",
                        torPath,
                        $"Tor{idx}");
                }
                else
                {
                    using (var proc = ProcessService.StartProcessDirect(
                                GetAppPath(@"Data\TorBin\tor.exe"),
                                $"-f {torrcFile}",
                                torPath))
                    {
                        if (proc != null) newPid = proc.Id;
                    }
                }
            });

            if (newPid.HasValue) _torPids[idx - 1] = newPid.Value;

            if (_torPids[idx - 1].HasValue)
            {
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

            await Task.Delay(1500);
        }

        if (_state.AbortBoot) return;


    }


    private void BootstrapTick(string selConfig, DateTime[] nextWarningTime, int warningInterval, DateTime startTime)
    {
        if (_bootstrapTimer == null) return; 

        if (_state.AbortBoot)
        {
            _bootstrapTimer?.Stop();
            _bootstrapTimer = null;
            if (txtConnectBtn != null)
            {
                txtConnectBtn.Text = CrimsonOnion.Localization.AppStrings.Connect;
                txtConnectBtn.Foreground = BrWhite;
            }
            SetConnectButtonProgress(-1);
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
            if (DateTime.Now >= nextWarningTime[0])
            {
                double elapsed = Math.Round((DateTime.Now - startTime).TotalSeconds, 1);
                CrimsonOnion.Services.SimpleLogger.Log($"[Bootstrap] Still waiting after {elapsed}s. Bridge={_activeBridge}, Config={_cfg.LastConfig}, Mode={_pollMode}");
                
                int elapsedMins = (int)Math.Round(elapsed / 60.0);
                ShowToast(string.Format(CrimsonOnion.Localization.AppStrings.ToastBootstrappingLongFormat, elapsedMins));
                
                nextWarningTime[0] = DateTime.Now.AddSeconds(warningInterval);
            }
            if (bestPct >= 0 && txtConnectBtn != null)
            {
                txtConnectBtn.Text = CrimsonOnion.Localization.AppStrings.BtnConnecting;
                SetConnectButtonProgress(bestPct);
            }
            return;
        }

        _bootstrapTimer?.Stop();
        _bootstrapTimer = null;

        TryDeleteFile(GetAppPath(@"Data\Xray\access.log"));
        TryDeleteFile(GetAppPath(@"Data\Xray\error.log"));

        _xrayBootTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _xrayBootTimer.Tick += async (s2, e2) =>
        {
            if (s2 is DispatcherTimer timer) timer.Stop();
            if (_state.AbortBoot) return;

            int? newXrayPid = null;
            int? newSbPid = null;
            bool success = true;

            await Task.Run(() =>
            {
                if (!XrayConfigWriter.Write(_cfg, _cfg.XrayDir)) { success = false; return; }

                if (_pollMode == "VPN Mode")
                {
                    if (!SingboxConfigWriter.Write(_cfg, _cfg.SbDir)) { success = false; return; }
                }

                if (_cfg.DebugMode)
                    newXrayPid = StartDebugProcess(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir, "Xray");
                else
                {
                    using var p = ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir);
                    newXrayPid = p?.Id;
                }

                if (_pollMode == "VPN Mode")
                {
                    if (_cfg.DebugMode)
                        newSbPid = StartDebugProcess(GetAppPath(@"Data\sing_box\sing-box.exe"), "run -c config.json", _cfg.SbDir, "SingBox");
                    else
                    {
                        using var p = ProcessService.StartProcessDirect(GetAppPath(@"Data\sing_box\sing-box.exe"), "run -c config.json", _cfg.SbDir);
                        newSbPid = p?.Id;
                    }
                }
            });

            if (!success) return;

            if (_cfg.DebugMode)
            {
                _xrayDebugPid = newXrayPid;
                if (_pollMode == "VPN Mode") _sbDebugPid = newSbPid;
            }
            else
            {
                _xrayPid = newXrayPid;
                if (_pollMode == "VPN Mode") _sbPid = newSbPid;
            }

            ProxyService.SetSystemProxy(_pollMode == "Proxy Mode");

            _state.IsConnected      = true;
            _state.SessionStartTime = DateTime.Now;
            CrimsonOnion.Services.SimpleLogger.Log($"[Connect] Bridge={_activeBridge}, Config={_cfg.LastConfig}, Mode={_pollMode}, Tor instances={_activeTorEngines}");
            SetConnectButtonProgress(100);
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


    private async Task StartBackendXrayAsync()
    {
        bool useAdapter = _cfg.EnableAdapterBinding && !string.IsNullOrWhiteSpace(_cfg.SelectedAdapterIp);
        bool useOutbound = _cfg.EnableOutboundProxy && !string.IsNullOrEmpty(_cfg.OutboundProxyAddress) && !string.IsNullOrEmpty(_cfg.OutboundProxyPort);

        if (!useAdapter && !useOutbound) return;

        var adapterXrayDir = GetAppPath(@"Data\Xray");
        if (!Directory.Exists(adapterXrayDir)) Directory.CreateDirectory(adapterXrayDir);

        var configObj = new Newtonsoft.Json.Linq.JObject
        {
            ["log"] = new Newtonsoft.Json.Linq.JObject { ["loglevel"] = "warning" },
            ["inbounds"] = new Newtonsoft.Json.Linq.JArray
            {
                new Newtonsoft.Json.Linq.JObject
                {
                    ["port"] = 10819,
                    ["listen"] = "127.0.0.1",
                    ["protocol"] = "socks",
                    ["settings"] = new Newtonsoft.Json.Linq.JObject { ["auth"] = "noauth", ["udp"] = true }
                }
            }
        };

        var outbounds = new Newtonsoft.Json.Linq.JArray();
        
        var boundOutbound = new Newtonsoft.Json.Linq.JObject
        {
            ["tag"] = "bound_out"
        };

        if (useAdapter)
        {
            boundOutbound["protocol"] = "freedom";
            boundOutbound["settings"] = new Newtonsoft.Json.Linq.JObject();
            boundOutbound["sendThrough"] = _cfg.SelectedAdapterIp;
        }
        else if (useOutbound)
        {
            boundOutbound["protocol"] = _cfg.OutboundProxyType == "HTTPS" ? "http" : "socks";
            
            var serverObj = new Newtonsoft.Json.Linq.JObject
            {
                ["address"] = _cfg.OutboundProxyAddress,
                ["port"] = int.TryParse(_cfg.OutboundProxyPort, out int p) ? p : 1080
            };

            if (_cfg.EnableOutboundAuth && !string.IsNullOrEmpty(_cfg.OutboundProxyUser) && !string.IsNullOrEmpty(_cfg.OutboundProxyPass))
            {
                var userObj = new Newtonsoft.Json.Linq.JObject
                {
                    ["user"] = _cfg.OutboundProxyUser,
                    ["pass"] = _cfg.OutboundProxyPass
                };
                serverObj["users"] = new Newtonsoft.Json.Linq.JArray { userObj };
            }

            boundOutbound["settings"] = new Newtonsoft.Json.Linq.JObject
            {
                ["servers"] = new Newtonsoft.Json.Linq.JArray { serverObj }
            };
        }
        
        outbounds.Add(boundOutbound);

        var directOutbound = new Newtonsoft.Json.Linq.JObject
        {
            ["protocol"] = "freedom",
            ["tag"] = "direct_out",
            ["settings"] = new Newtonsoft.Json.Linq.JObject()
        };
        outbounds.Add(directOutbound);

        var routingObj = new Newtonsoft.Json.Linq.JObject
        {
            ["domainStrategy"] = "AsIs"
        };
        
        var rulesArray = new Newtonsoft.Json.Linq.JArray();
        var localRule = new Newtonsoft.Json.Linq.JObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct_out"
        };
        localRule["ip"] = new Newtonsoft.Json.Linq.JArray { "127.0.0.0/8", "::1/128", "geoip:private" };
        rulesArray.Add(localRule);

        var allRule = new Newtonsoft.Json.Linq.JObject
        {
            ["type"] = "field",
            ["network"] = "tcp,udp",
            ["outboundTag"] = "bound_out"
        };
        rulesArray.Add(allRule);

        routingObj["rules"] = rulesArray;

        configObj["outbounds"] = outbounds;
        configObj["routing"] = routingObj;

        int? newAdapterPid = null;
        int? newAdapterDebugPid = null;

        await Task.Run(() =>
        {
            string configJson = configObj.ToString();
            File.WriteAllText(Path.Combine(adapterXrayDir, "adapter_config.json"), configJson);
            
            if (_cfg.DebugMode)
            {
                newAdapterDebugPid = StartDebugProcess(GetAppPath(@"Data\Xray\xray.exe"), "run -c adapter_config.json", adapterXrayDir, "AdapterXray");
            }
            else
            {
                using var p = ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c adapter_config.json", adapterXrayDir);
                newAdapterPid = p?.Id;
            }
        });

        if (_cfg.DebugMode)
        {
            _adapterXrayDebugPid = newAdapterDebugPid;
        }
        else
        {
            _adapterXrayPid = newAdapterPid;
        }
    }

    private void SmartRestartXray()
    {
        CrimsonOnion.Services.SimpleLogger.Log("SmartRestartXray called from: " + new System.Diagnostics.StackTrace().ToString());
        if (_cfg.LastXrayMode == "VPN Mode")
        {
            if (_state.IsConnected)
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
            else if (_state.IsEngineRunning)
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
            return;
        }

        if (_state.IsEngineRunning || _state.IsConnected)
        {
            RestartXray(_cfg.LastXrayMode);
        }
    }

    private string _xrayRestartTargetMode = "";

    private void RestartXray(string targetMode)
    {
        int? debugPid = _xrayDebugPid; _xrayDebugPid = null;
        int? mainPid = _xrayPid; _xrayPid = null;
        
        Task.Run(() => {
            KillPid(debugPid);
            KillPid(mainPid);
        });

        _xrayRestartTargetMode = targetMode;
        
        if (_xrayRestartTimer == null)
        {
            _xrayRestartTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _xrayRestartTimer.Tick += OnXrayRestartTick;
        }

        _xrayRestartTimer.Stop();
        _xrayRestartTimer.Start();
    }

    private async void OnXrayRestartTick(object? sender, EventArgs e)
    {
        _xrayRestartTimer?.Stop();
        string targetMode = _xrayRestartTargetMode;
        
        int? newDebugPid = null;
        int? newMainPid = null;
        bool success = true;

        await Task.Run(() =>
        {
            if (!XrayConfigWriter.Write(_cfg, _cfg.XrayDir)) { success = false; return; }

            if (_cfg.DebugMode)
                newDebugPid = StartDebugProcess(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir, "Xray");
            else
            {
                using var p = ProcessService.StartProcessDirect(GetAppPath(@"Data\Xray\xray.exe"), "run -c config.json", _cfg.XrayDir);
                newMainPid = p?.Id;
            }
        });

        if (!success) return;

        _xrayDebugPid = newDebugPid;
        _xrayPid = newMainPid;

        ProxyService.SetSystemProxy(targetMode == "Proxy Mode");

        if (_state.IsConnected)
        {
            await UpdateLanIpAsync();
            UpdateLanPortUI();

            UpdateLocalPortUI();

            if (_pingCts != null) { try { _pingCts.Cancel(); _pingCts.Dispose(); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _pingCts = null; }
                            _pingCts = new System.Threading.CancellationTokenSource();
            var pToken = _pingCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1500, pToken).ConfigureAwait(false);
                    if (!pToken.IsCancellationRequested) 
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                        {
                            if (!pToken.IsCancellationRequested) StartGeoPing();
                        });
                    }
                }
                catch { }
            });
        }
    }


    private global::Avalonia.Controls.TextBlock? _lblTimerCache;
    private void StartSessionClock()
    {
        var panTimerContent = this.FindControl<StackPanel>("panTimerContent");
        if (panTimerContent != null) panTimerContent.IsVisible = true;
        var lblDisconnected = this.FindControl<TextBlock>("lblDisconnected");
        if (lblDisconnected != null) lblDisconnected.IsVisible = false;

        if (_sessionClockTimer == null)
        {
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
                if (_lblTimerCache == null) _lblTimerCache = this.FindControl<TextBlock>("lblTimer");
                if (_lblTimerCache != null)
                    _lblTimerCache.Text = elapsed.ToString(@"hh\:mm\:ss");
            };
        }
        else
        {
            _sessionClockTimer.Stop();
        }
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
        if (txtCustomBridge == null || string.IsNullOrWhiteSpace(txtCustomBridge.Text))
        {
            var panCustomBridge = this.FindControl<global::Avalonia.Controls.Border>("panCustomBridge");
            if (panCustomBridge != null)
            {
                panCustomBridge.MaxHeight       = 0;
                panCustomBridge.Opacity         = 0;
                panCustomBridge.BorderThickness = new global::Avalonia.Thickness(0);
            }
            ApplyLoadedSettings();
            UpdateAdvancedBridgesUI(false);
            return;
        }

        if (_cfg != null && _state != null)
        {
            _cfg.CustomBridgeLine = txtCustomBridge.Text.Trim();
            _activeBridge = "Custom";
            _cfg.LastBridge = _activeBridge;
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
            RequestConfigSave();
            
            UpdateAdvancedBridgesUI();

            bool isDnsttBypass = (_cfg.EnableAdapterBinding || _cfg.EnableOutboundProxy) && 
                                 _cfg.CustomBridgeLine.IndexOf("dnstt", StringComparison.OrdinalIgnoreCase) >= 0;
            
            if (_state.IsEngineRunning)
            {
                if (isDnsttBypass)
                {
                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastDnsttBridgeWarning1);
                }
                else
                {
                    ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            else if (isDnsttBypass)
            {
                ShowToast(CrimsonOnion.Localization.AppStrings.ToastDnsttBridgeWarning2);
            }
        }
        
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panCustomBridge");
        if (pan != null)
        {
            pan.MaxHeight       = 0;
            pan.Opacity         = 0;
            pan.BorderThickness = new global::Avalonia.Thickness(0);
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
        ApplyLoadedSettings();
        UpdateAdvancedBridgesUI(false);
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

        var oldClient = _httpClient;
        System.Net.Http.HttpClient newClient;
        try
        {
            var sysProxy = System.Net.WebRequest.GetSystemWebProxy();
            sysProxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            newClient = new System.Net.Http.HttpClient(
                new System.Net.Http.HttpClientHandler { Proxy = sysProxy, UseProxy = true });
        }
        catch
        {
            newClient = new System.Net.Http.HttpClient();
        }
        newClient.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
        
        _httpClient = newClient;
        oldClient?.Dispose();
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
                            (imgCaptcha.Source as global::Avalonia.Media.Imaging.Bitmap)?.Dispose();
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
            catch (Exception ex)
            {
                CrimsonOnion.Services.SimpleLogger.Log(ex);
                if (!_fetchingBridges) return;
            }
        }
        
        if (_fetchingBridges)
        {
            CancelFetch();
            _ = Dispatcher.UIThread.InvokeAsync(() => ShowToast(CrimsonOnion.Localization.AppStrings.ToastFailedToReachTor));
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
                _ = Dispatcher.UIThread.InvokeAsync(() => ShowToast(CrimsonOnion.Localization.AppStrings.ToastFailedToReachTor));
            }
        }
        catch (Exception ex)
        {
            CrimsonOnion.Services.SimpleLogger.Log(ex);
            CancelFetch();
            _ = Dispatcher.UIThread.InvokeAsync(() => ShowToast(CrimsonOnion.Localization.AppStrings.ToastFailedToReachTor));
        }
    }


    private global::Avalonia.Threading.DispatcherTimer? _logTimer;
    private global::Avalonia.Threading.DispatcherTimer? _logClearTimer;
    private long _lastXrayLogPos = 0;
    private int _isReadingLogs = 0; 
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
                    try { using var fs = new FileStream(fp, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }
            }
        };
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
            if (_torLabels == null) _torLabels = new[] { lblTor1, lblTor2, lblTor3, lblTor4, lblTor5, lblTor6, lblTor7, lblTor8 };
            for (int i = 1; i <= 8; i++)
            {
                var lbl = _torLabels[i - 1];
                if (lbl != null)
                {
                    var padded = i.ToString().PadLeft(2, '0');
                    lbl.Text = i <= selCount ? $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.TorStatusOffline}" : $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.Disabled}";
                    lbl.Foreground = BrGray; 
                    lbl.Opacity = 0.5;
                }
            }
            var txtLogs = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayLogs");
            if (txtLogs != null) txtLogs.Text = "";
            return;
        }

        if (_torLabels == null) _torLabels = new[] { lblTor1, lblTor2, lblTor3, lblTor4, lblTor5, lblTor6, lblTor7, lblTor8 };
        for (int i = 1; i <= 8; i++)
        {
            var lbl = _torLabels[i - 1];
            if (lbl != null)
            {
                var padded = i.ToString().PadLeft(2, '0');
                int uiSelCount = _activeTorEngines;
                if (i > uiSelCount && i > _pollSelCount)
                {
                    lbl.Text = $"TOR {padded}: {CrimsonOnion.Localization.AppStrings.Disabled}";
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

        if (System.Threading.Interlocked.CompareExchange(ref _isReadingLogs, 1, 0) != 0) return;
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
                    long currentPos = Interlocked.Read(ref _lastXrayLogPos);
                    if (fs.Length < currentPos) { Interlocked.Exchange(ref _lastXrayLogPos, 0); currentPos = 0; }
                    if (fs.Length > currentPos)
                    {
                        fs.Seek(currentPos, SeekOrigin.Begin);
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
                        Interlocked.Exchange(ref _lastXrayLogPos, fs.Length);
                    }
                }
                catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }
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
            finally { System.Threading.Interlocked.Exchange(ref _isReadingLogs, 0); }
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
        CrimsonOnion.Services.SimpleLogger.Log("StartGeoPing called from: " + new System.Diagnostics.StackTrace().ToString());
        _state.IsGeoTracing = true;

        var lblCountry = this.FindControl<TextBlock>("lblCountryName");
        var lblPing    = this.FindControl<TextBlock>("lblPing");
        if (lblCountry != null) lblCountry.Text = CrimsonOnion.Localization.AppStrings.GeoTracing;
        if (lblPing    != null) lblPing.Text    = "0 ms";

        if (_geoCts != null) { try { _geoCts.Cancel(); _geoCts.Dispose(); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } }
        _geoCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = _geoCts.Token;

        _ = Task.Run(async () =>
        {
            var geo = await CrimsonOnion.Services.NetworkDiagnosticsService.FetchGeoAsync(token).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _state.IsGeoTracing = false;
                if (!_state.IsConnected) return;

                if (geo == null)
                {
                    if (lblCountry != null) lblCountry.Text = CrimsonOnion.Localization.AppStrings.GeoTimeout;
                    if (lblPing    != null) lblPing.Text    = "0 ms";
                    return;
                }

                // Map continent code to display name 
                var continentDisplay = _continentNames.TryGetValue(geo.ContinentCode, out var c) ? c : geo.ContinentCode;
                var countryDisplay   = geo.Country;
                var continentCode    = geo.ContinentCode;
                var countryCode      = geo.CountryCode;

                bool isFa = CrimsonOnion.Localization.AppStrings.IsPersian;
                if (isFa)
                {
                    continentDisplay = CrimsonOnion.Localization.GeoTranslation.GetContinentFa(continentCode, continentDisplay);
                    countryDisplay   = CrimsonOnion.Localization.GeoTranslation.GetCountryFa(countryCode, countryDisplay);
                }

                string geoStr;
                if (_cfg.EnableV2rayChain || _cfg.LastConfig == "Custom")
                    geoStr = countryDisplay;
                else if (_cfg.LastConfig == "Expert"
                         && !string.IsNullOrWhiteSpace(_cfg.ExpertExitNodes)
                         && !_cfg.ExpertExitNodes.Contains(","))
                    geoStr = countryDisplay;
                else if (_cfg.LastConfig != "Optimized" && _cfg.LastConfig != "Expert")
                    geoStr = countryDisplay;
                else
                    geoStr = continentDisplay;

                if (string.IsNullOrWhiteSpace(geoStr)) geoStr = "Unknown";

                if (lblCountry != null) lblCountry.Text = geoStr.ToUpper();
                if (lblPing    != null) lblPing.Text    = $"{geo.PingMs}ms";
            });
        }, token);
    }



    private void StartStatsPolling()
    {
        UpdateLanPortUI();
        _logClearTimer?.Stop();
        _logClearTimer?.Start();

        if (_statsCts != null) { try { _statsCts.Cancel(); _statsCts.Dispose(); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); } _statsCts = null; }
                _statsCts = new System.Threading.CancellationTokenSource();
        var token = _statsCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1500, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;
                
                try { PollStatsTick(); } catch { }
            }
        }, token);
    }

    private void PollStatsTick()
    {
        if (!_state.IsConnected || System.Threading.Interlocked.CompareExchange(ref _isFetchingStatsInt, 1, 0) != 0) return;

        Task.Run(async () =>
        {
            try
            {
                var (curUpBytes, curDnBytes) = await CrimsonOnion.Services.NetworkDiagnosticsService
                    .FetchStatsAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);

                if (curUpBytes < 0) return; 

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

                    var now     = DateTime.UtcNow;
                    double elapsed = (now - _lastPollTime).TotalSeconds;
                    if (elapsed <= 0) elapsed = 1.0;

                    double curSpdUp = diffUp / elapsed;
                    double curSpdDn = diffDn / elapsed;

                    string spdUp = curSpdUp >= 1048576 ? $"{Math.Round(curSpdUp / 1048576.0, 2)} MB/s"
                                 : curSpdUp >= 1024    ? $"{Math.Round(curSpdUp / 1024.0, 1)} KB/s"
                                 :                       $"{(int)curSpdUp} B/s";
                    string spdDn = curSpdDn >= 1048576 ? $"{Math.Round(curSpdDn / 1048576.0, 2)} MB/s"
                                 : curSpdDn >= 1024    ? $"{Math.Round(curSpdDn / 1024.0, 1)} KB/s"
                                 :                       $"{(int)curSpdDn} B/s";
                    string tot   = _state.SessionDataBytes >= 1073741824
                                       ? $"{Math.Round(_state.SessionDataBytes / 1073741824.0, 2)} GB"
                                  : _state.SessionDataBytes >= 1048576
                                       ? $"{Math.Round(_state.SessionDataBytes / 1048576.0, 1)} MB"
                                  :      $"{Math.Round(_state.SessionDataBytes / 1024.0, 1)} KB";

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (lblTotalData      != null) lblTotalData.Text      = tot;
                        if (lblDownloadSpeed  != null) lblDownloadSpeed.Text  = spdDn;
                        if (lblUploadSpeed    != null) lblUploadSpeed.Text    = spdUp;
                        DrawGraph();
                    });
                }

                if (curUpBytes > 0) _lastUpBytes = curUpBytes;
                if (curDnBytes > 0) _lastDnBytes = curDnBytes;
                _lastPollTime = DateTime.UtcNow;
            }
            catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }
            finally { System.Threading.Interlocked.Exchange(ref _isFetchingStatsInt, 0); }
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
        int count = Math.Min(_upHistory.Count, _dnHistory.Count);
        if (count < 2) return;

        double step   = width / (40 - 1);
        double maxUp  = _upHistory.Count > 0 ? _upHistory.Max() : 0;
        double maxDn  = _dnHistory.Count > 0 ? _dnHistory.Max() : 0;
        double maxVal = Math.Max(maxUp, maxDn);
        if (maxVal < 1024) maxVal = 1024;

        _ptsUpCache.Clear();
        _ptsDnCache.Clear();

        int startIdx = 40 - count;
        double drawHeight = height - topPadding - bottomPadding;

        using var upEnum = _upHistory.GetEnumerator();
        using var dnEnum = _dnHistory.GetEnumerator();

        for (int i = 0; i < count; i++)
        {
            if (!upEnum.MoveNext() || !dnEnum.MoveNext()) break;
            double x = (startIdx + i) * step;
            double yUp = (height - bottomPadding) - (upEnum.Current / maxVal * drawHeight);
            double yDn = (height - bottomPadding) - (dnEnum.Current / maxVal * drawHeight);
            _ptsUpCache.Add(new global::Avalonia.Point(x, yUp));
            _ptsDnCache.Add(new global::Avalonia.Point(x, yDn));
        }

        graphUpload.Data = GenerateSmoothSpline(_ptsUpCache, false, width, height);
        graphDownload.Data = GenerateSmoothSpline(_ptsDnCache, false, width, height);
        graphUploadFill.Data = GenerateSmoothSpline(_ptsUpCache, true, width, height);
        graphDownloadFill.Data = GenerateSmoothSpline(_ptsDnCache, true, width, height);

                var canvas = graphUpload.Parent as global::Avalonia.Controls.Canvas;
        if (canvas != null && canvas.RenderTransform is global::Avalonia.Media.TranslateTransform t)
        {
            _graphTranslate = t;
            t.X = 0;
            _graphTargetX = -step;
            _graphStepX = step / (1000.0 / 33.0);
            
            if (_graphTimer == null)
            {
                _graphTimer = new global::Avalonia.Threading.DispatcherTimer();
                _graphTimer.Interval = TimeSpan.FromMilliseconds(33);
                _graphTimer.Tick += (s, e) => {
                    if (!this.IsActive) return;
                    if (_graphTranslate != null && _graphTranslate.X > _graphTargetX)
                    {
                        _graphTranslate.X -= _graphStepX;
                        if (_graphTranslate.X < _graphTargetX) _graphTranslate.X = _graphTargetX;
                    }
                };
                _graphTimer.Start();
            }
        }
    }

    private global::Avalonia.Media.StreamGeometry GenerateSmoothSpline(System.Collections.Generic.List<global::Avalonia.Point> points, bool isFill, double width, double height)
    {
        var geom = new global::Avalonia.Media.StreamGeometry();
        using (var ctx = geom.Open())
        {
            if (points.Count == 0) return geom;
            
            if (isFill)
            {
                ctx.BeginFigure(new global::Avalonia.Point(points[0].X, height), true);
                ctx.LineTo(points[0]);
            }
            else
            {
                ctx.BeginFigure(points[0], false);
            }

            for (int i = 1; i < points.Count; i++)
            {
                var p0 = i >= 2 ? points[i - 2] : points[i - 1];
                var p1 = points[i - 1];
                var p2 = points[i];
                var p3 = i + 1 < points.Count ? points[i + 1] : points[i];

                double t = 0.25;
                var cp1 = new global::Avalonia.Point(p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t);
                var cp2 = new global::Avalonia.Point(p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t);

                ctx.CubicBezierTo(cp1, cp2, p2);
            }

            if (isFill)
            {
                ctx.LineTo(new global::Avalonia.Point(points[points.Count - 1].X, height));
            }
        }
        return geom;
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


        private void btnSplitTunnel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panSplitOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSplitOverlay");
        if (panSplitOverlay != null && !panSplitOverlay.IsVisible)
        {
            CloseAllOverlays();
            panSplitOverlay.IsVisible = true;
            panSplitOverlay.Classes.Add("popupOpen");
            var ldo = this.FindControl<global::Avalonia.Controls.Border>("LightDismissOverlay");
            if (ldo != null) ldo.IsVisible = true;
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


        private void btnSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var panSettingsOverlay = this.FindControl<global::Avalonia.Controls.Border>("panSettingsOverlay");
        if (panSettingsOverlay != null && !panSettingsOverlay.IsVisible)
        {
            CloseAllOverlays();
            panSettingsOverlay.IsVisible = true;
            panSettingsOverlay.Classes.Add("popupOpen");
            var ldo = this.FindControl<global::Avalonia.Controls.Border>("LightDismissOverlay");
            if (ldo != null) ldo.IsVisible = true;
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
                    nic = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(a => a.Name == _cfg.SelectedAdapterName && a.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up);
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









