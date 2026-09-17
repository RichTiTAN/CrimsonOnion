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
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CrimsonOnion.Localization;
using CrimsonOnion.Models;
using CrimsonOnion.Services;

namespace CrimsonOnion.Views
{
    internal sealed class ConnectFlow
    {
        internal const string ConnectLabel = "txtConnectBtn";
        internal const string ConnectedLabel = "txtConnectedBtn";
        internal const string RingIdle = "Idle";
        internal const string RingConnecting = "Connecting";
        internal const string RingConnected = "Connected";
        internal const string DirectBridge = BridgeNames.Direct;
        internal const string IdlePingText = "0 ms";
        internal const string IdleTotalText = "0 MB";
        internal const string IdleSpeedText = "0 KB/s";
        internal const string Tor1StatePath = @"Data\Tors\Tor1\Data\state";
        private const int FillReset = -1;
        private const int FillFrameMs = 16;
        private const double FillEase = 0.35;
        private const double FillSettle = 0.005;
        private const double FillEmpty = 0.001;
        private const double FillComplete = 0.999;
        private const double FillFull = 1.0;
        private const string FillOrange = "#DD6B20";
        private static readonly TimeSpan BootSettle = TimeSpan.FromMilliseconds(800);
        private const int BridgedWarningSeconds = 300;
        private const int DirectWarningSeconds = 180;
        private static readonly TimeSpan PingAfterRestart = TimeSpan.FromMilliseconds(1500);
        private const int KillGraceMs = 3000;
        private const int CountToastMs = 3000;
        private const string LogBox = "txtXrayLogs";
        private const string PingLabel = "lblPing";
        private const string TotalLabel = "lblTotalData";
        private const string DownloadLabel = "lblDownloadSpeed";
        private const string UploadLabel = "lblUploadSpeed";
        private const string TimerLabel = "lblTimer";
        private const string CountryLabel = "lblCountryName";
        private const string GraphDownloadShape = "graphDownload";
        private const string GraphUploadShape = "graphUpload";
        private const string GraphDownloadFillShape = "graphDownloadFill";
        private const string GraphUploadFillShape = "graphUploadFill";
        private static readonly SolidColorBrush IdleLabelBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));

        private readonly AppConfig _cfg;
        private readonly AppState _state;
        private readonly VpnRuntimeState _vpn;
        private readonly BootstrapOrchestrator _bootstrap;
        private readonly TorFarmService _torFarm;
        private readonly XraySupervisor _xray;
        private readonly TorLauncherService _tor;
        private readonly TrafficStatsService _traffic;
        private readonly GraphScroller _graphScroll;

        private readonly Func<string, Control?> _find;
        private readonly Func<string> _bridge;
        private readonly Func<string> _mode;
        private readonly Func<int> _engines;
        private readonly Action<string> _toast;
        private readonly Action<string> _ring;
        private readonly Action _save;
        private readonly Func<Task> _updateLanIp;
        private readonly Func<Task> _applyDns;
        private readonly Func<Task> _restoreDns;
        private readonly Action _refreshPorts;
        private readonly Action _startSessionPanels;
        private readonly Action _startGeoPing;
        private readonly Action _startLogs;
        private readonly Action _stopLogs;
        private readonly Action _resetLogTailer;

        private DispatcherTimer? _fillAnimTimer;
        private double _currentFillPct = 0;
        private double _targetFillPct = -1;

        private TextBlock? _fillTxtBg;
        private TextBlock? _fillTxtConnected;
        private LinearGradientBrush? _fillBrush;
        private GradientStop? _fillStop1;
        private GradientStop? _fillStop2;
        private GradientStop? _fillStop3;
        private GradientStop? _fillStop4;
        private string _restartMode = "";
        private long _lastCountToastTick = 0;
        internal ConnectFlow(
            AppConfig cfg,
            AppState state,
            VpnRuntimeState vpn,
            BootstrapOrchestrator bootstrap,
            TorFarmService torFarm,
            XraySupervisor xray,
            TorLauncherService tor,
            TrafficStatsService traffic,
            GraphScroller graphScroll,
            Func<string, Control?> find,
            Func<string> bridge,
            Func<string> mode,
            Func<int> engines,
            Action<string> toast,
            Action<string> ring,
            Action save,
            Func<Task> updateLanIp,
            Func<Task> applyDns,
            Func<Task> restoreDns,
            Action refreshPorts,
            Action startSessionPanels,
            Action startGeoPing,
            Action startLogs,
            Action stopLogs,
            Action resetLogTailer)
        {
            _cfg                = cfg;
            _state              = state;
            _vpn                = vpn;
            _bootstrap          = bootstrap;
            _torFarm            = torFarm;
            _xray               = xray;
            _tor                = tor;
            _traffic            = traffic;
            _graphScroll        = graphScroll;
            _find               = find;
            _bridge             = bridge;
            _mode               = mode;
            _engines            = engines;
            _toast              = toast;
            _ring               = ring;
            _save               = save;
            _updateLanIp        = updateLanIp;
            _applyDns           = applyDns;
            _restoreDns         = restoreDns;
            _refreshPorts       = refreshPorts;
            _startSessionPanels = startSessionPanels;
            _startGeoPing       = startGeoPing;
            _startLogs          = startLogs;
            _stopLogs           = stopLogs;
            _resetLogTailer     = resetLogTailer;
        }
        internal void ConnectClicked()
        {
            if (_state.IsConnected || _state.IsEngineRunning)
            {
                Stop();
                return;
            }

            if (_cfg.LastXrayMode == XraySupervisor.VpnMode)
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
                        _toast(AppStrings.ToastVpnAdapterInUse);
                        return;
                    }
                }
                catch (Exception ex) { SimpleLogger.Log(ex); }
            }

            if (!File.Exists(AppPath(Tor1StatePath)))
            {
                _toast(AppStrings.ToastFirstConnectionLong);
            }

            if (_cfg.EnableAdapterBinding && !string.IsNullOrWhiteSpace(_cfg.SelectedAdapterName)
                && !SplitTunnelService.IsAdapterUp(_cfg.SelectedAdapterName))
            {
                _toast(AppStrings.ToastAdapterNotAvailable);
                return;
            }

            if (_cfg.EnableDirectUDP && !string.IsNullOrWhiteSpace(_cfg.DirectUdpAdapterName)
                && _cfg.DirectUdpAdapterName != SplitTunnelService.DefaultAdapter
                && !SplitTunnelService.IsAdapterUp(_cfg.DirectUdpAdapterName))
            {
                _toast(AppStrings.ToastDirectUdpAdapterNotAvailable);
                return;
            }

            StartAsync();
        }
        internal void EnginesChanged(int newCount)
        {
            _save();

            if (_torFarm.ApplyEngineCount(newCount) != EngineCountOutcome.ReconnectRequired) return;

            long now = Environment.TickCount64;
            if (now - _lastCountToastTick > CountToastMs)
            {
                _lastCountToastTick = now;
                _toast(AppStrings.ToastReconnectChanges);
            }
        }
        private string AppPath(string relative) => Path.Combine(_cfg.BaseDir, relative);
        private void SetLabel(string name, string text)
        {
            if (_find(name) is TextBlock label) label.Text = text;
        }
        private void SetBoxText(string name, string text)
        {
            if (_find(name) is TextBox box) box.Text = text;
        }
        internal void Stop(bool isClosing = false)
        {
            _state.AbortBoot       = true;
            _state.IsEngineRunning = false;

            _torFarm.ResetProgress();
            _torFarm.RefreshSlots();

            _bootstrap.CancelSequence();
            _torFarm.StopLaunching();
            _traffic.Stop();
            CancellationTokens.CancelAndDispose(ref _vpn.PingCts);
            CancellationTokens.CancelAndDispose(ref _vpn.GeoCts);
            _graphScroll.Stop();
            _stopLogs();
            ProxyService.SetSystemProxy(false);
            _ = _restoreDns();

            _tor.StopAll();

            SetProgress(FillReset);

            var killTask = Task.Run(() => {
                _torFarm.KillFleet();
                _xray.StopAll();
                _torFarm.ClearStaleArtifacts();
                _xray.DeleteLogs(rotated: true);
            });
            if (isClosing)
            {
                killTask.Wait(KillGraceMs);
                JobManager.Shutdown();
            }

            SimpleLogger.Log($"[Disconnect] Bridge={_bridge()}, Mode={_mode()}, isClosing={isClosing}");

            _state.IsConnected      = false;
            _state.LastTotalBytes   = 0;
            _state.SessionDataBytes = 0;
            _state.SessionStartTime = null;
            _state.SpeedSamples     = _state.SpeedSamples ?? new double[5];
            Array.Clear(_state.SpeedSamples, 0, _state.SpeedSamples.Length);

            Dispatcher.UIThread.Post(() =>
            {
                _traffic.Clear();
                ClearGraph();

                SetLabel(PingLabel, IdlePingText);

                _refreshPorts();

                SetLabel(TimerLabel, AppStrings.Disconnected);
                SetLabel(CountryLabel, AppStrings.Disconnected);
            });

            if (!isClosing)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowConnectLabel();

                    _torFarm.RefreshSlots();

                    SetBoxText(LogBox, "");

                    SetLabel(TotalLabel, IdleTotalText);
                    SetLabel(DownloadLabel, IdleSpeedText);
                    SetLabel(UploadLabel, IdleSpeedText);
                    SetLabel(PingLabel, IdlePingText);

                    _ring(RingIdle);
                });
            }
        }
        private void ClearGraph()
        {
            if (_find(GraphUploadShape) is Avalonia.Controls.Shapes.Path graphUpload) graphUpload.Data = null;
            if (_find(GraphDownloadShape) is Avalonia.Controls.Shapes.Path graphDownload) graphDownload.Data = null;
            if (_find(GraphUploadFillShape) is Avalonia.Controls.Shapes.Path graphUploadFill) graphUploadFill.Data = null;
            if (_find(GraphDownloadFillShape) is Avalonia.Controls.Shapes.Path graphDownloadFill) graphDownloadFill.Data = null;
        }
        private void ShowConnectLabel()
        {
            if (_find(ConnectLabel) is TextBlock connectLabel)
            {
                connectLabel.Text = AppStrings.Connect;
                connectLabel.Foreground = IdleLabelBrush;
            }
        }
        private void EnsureFillResources()
        {
            if (_fillTxtBg == null) _fillTxtBg = _find(ConnectLabel) as TextBlock;
            if (_fillTxtConnected == null) _fillTxtConnected = _find(ConnectedLabel) as TextBlock;

            if (_fillBrush == null)
            {
                var orange = Color.Parse(FillOrange);
                _fillStop1 = new GradientStop(orange, 0.0);
                _fillStop2 = new GradientStop(orange, 0.0);
                _fillStop3 = new GradientStop(Colors.White, 0.0);
                _fillStop4 = new GradientStop(Colors.White, 1.0);
                _fillBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint   = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = new GradientStops { _fillStop1, _fillStop2, _fillStop3, _fillStop4 }
                };
            }
        }
        private void SetProgress(int percent)
        {
            if (percent < 0)
            {
                _targetFillPct = -1;
                _currentFillPct = 0;
                _fillAnimTimer?.Stop();

                EnsureFillResources();

                if (_fillTxtBg != null) { _fillTxtBg.Foreground = IdleLabelBrush; _fillTxtBg.Opacity = 1; }
                if (_fillTxtConnected != null) { _fillTxtConnected.Opacity = 0; }
                return;
            }

            if (_fillAnimTimer == null)
            {
                _fillAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FillFrameMs) };
                _fillAnimTimer.Tick += (s, e) =>
                {
                    if (_targetFillPct < 0) return;

                    double diff = _targetFillPct - _currentFillPct;
                    if (Math.Abs(diff) < FillSettle) _currentFillPct = _targetFillPct;
                    else _currentFillPct += diff * FillEase;

                    EnsureFillResources();

                    if (_fillTxtBg != null)
                    {
                        if (_currentFillPct <= FillEmpty)
                        {
                            _fillTxtBg.Foreground = IdleLabelBrush;
                        }
                        else
                        {
                            _fillStop2!.Offset = _currentFillPct;
                            _fillStop3!.Offset = _currentFillPct;
                            _fillTxtBg.Foreground = _fillBrush;
                        }
                    }

                    if (_currentFillPct >= FillComplete && _targetFillPct >= FillFull && _state.IsConnected)
                    {
                        if (_fillTxtBg != null && _fillTxtConnected != null)
                        {
                            _fillTxtConnected.Text = AppStrings.ConnectedBtn;
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

            _targetFillPct = Math.Clamp(percent / 100.0, 0.0, 1.0);
        }
        private bool ShowConnectingLabel(bool resetBrush)
        {
            if (_find(ConnectLabel) is not TextBlock connectLabel) return false;

            connectLabel.Text = AppStrings.BtnConnecting;
            if (resetBrush) connectLabel.Foreground = IdleLabelBrush;
            return true;
        }
        internal void StopFillAnimation()
        {
            _fillAnimTimer?.Stop();
            _fillAnimTimer = null;
        }
        private async void StartAsync()
        {
            try
            {
                await StartCoreAsync();
            }
            catch (Exception ex)
            {
                SimpleLogger.Log(ex);
                _toast(string.Format(AppStrings.ToastEngineStartFailedFormat, ex.Message));
                Stop();
            }
        }
        private async Task StartCoreAsync()
        {
            VpnEngineService.ClearSeenLogs();

            _bootstrap.CancelSequence();
            _torFarm.StopLaunching();

            _state.IsEngineRunning = true;
            _state.AbortBoot = false;

            ShowConnectingLabel(true);
            SetProgress(0);
            _ring(RingConnecting);

            await _updateLanIp();
            await _applyDns();

            _tor.StopAll();

            await Task.Run(() =>
            {
                _torFarm.KillFleet();
                _xray.StopAll();
            });

            ProxyService.SetSystemProxy(false);

            _state.IsConnected      = false;
            _state.LastTotalBytes   = 0;
            _state.SessionDataBytes = 0;
            _state.SessionStartTime = null;
            Array.Clear(_state.SpeedSamples, 0, _state.SpeedSamples.Length);

            _torFarm.SelectedCount  = _engines();
            _torFarm.SelectedBridge = _bridge();

            _torFarm.ResetProgress();
            _torFarm.RefreshSlots();

            _xray.DeleteLogs();
            SetBoxText(LogBox, "");
            _resetLogTailer();

            await Task.Delay(BootSettle);
            if (_state.AbortBoot) return;

            await Task.Run(() => _xray.StartAdapterBinding());

            _torFarm.ClearStaleArtifacts();

            bool isBridged  = _torFarm.SelectedBridge != DirectBridge;
            int warningInterval = isBridged ? BridgedWarningSeconds : DirectWarningSeconds;
            DateTime startTime = DateTime.Now;
            DateTime[] nextWarningTime = new[] { DateTime.Now.AddSeconds(warningInterval) };

            _bootstrap.StartProgressPolling(() => BootstrapTick(nextWarningTime, warningInterval, startTime));

            for (int i = 1; i <= _torFarm.SelectedCount; i++)
            {
                if (_state.AbortBoot) break;
                if (!_torFarm.CanLaunch) continue;

                await _torFarm.LaunchSlotAsync(i);

                await Task.Delay(_torFarm.SlotLaunchInterval);
            }

            if (_state.AbortBoot) return;
        }
        private void BootstrapTick(DateTime[] nextWarningTime, int warningInterval, DateTime startTime)
        {
            if (!_bootstrap.IsProgressPolling) return;

            if (_state.AbortBoot)
            {
                _bootstrap.StopProgressPolling();
                ShowConnectLabel();
                SetProgress(FillReset);
                _ring(RingIdle);
                return;
            }

            var (bestPct, oneReady) = _torFarm.SummarizeBoot();

            if (!oneReady)
            {
                if (DateTime.Now >= nextWarningTime[0])
                {
                    double elapsed = Math.Round((DateTime.Now - startTime).TotalSeconds, 1);
                    SimpleLogger.Log($"[Bootstrap] Still waiting after {elapsed}s. Bridge={_bridge()}, Config={_cfg.LastConfig}, Mode={_mode()}");

                    int elapsedMins = (int)Math.Round(elapsed / 60.0);
                    _toast(string.Format(AppStrings.ToastBootstrappingLongFormat, elapsedMins));

                    nextWarningTime[0] = DateTime.Now.AddSeconds(warningInterval);
                }
                if (bestPct >= 0 && ShowConnectingLabel(false))
                {
                    SetProgress(bestPct);
                }
                return;
            }

            _bootstrap.StopProgressPolling();

            _xray.DeleteLogs();

            _bootstrap.BootXrayOnce(async () =>
            {
                if (_state.AbortBoot) return;

                if (!await Task.Run(() => _xray.StartCore(_mode()))) return;

                ProxyService.SetSystemProxy(_mode() == XraySupervisor.ProxyMode);

                _state.IsConnected      = true;
                _state.SessionStartTime = DateTime.Now;
                SimpleLogger.Log($"[Connect] Bridge={_bridge()}, Config={_cfg.LastConfig}, Mode={_mode()}, Tor instances={_engines()}");
                SetProgress(100);
                _ring(RingConnected);

                _refreshPorts();

                _startSessionPanels();
                if (_state.IsLogsOpen) _startLogs();
            });
        }
        internal void RestartCoreForModeChange()
        {
            if (_cfg.LastXrayMode == XraySupervisor.VpnMode)
            {
                if (_state.IsConnected || _state.IsEngineRunning)
                    _toast(AppStrings.ToastReconnectChanges);
                return;
            }

            if (_state.IsEngineRunning || _state.IsConnected)
            {
                RestartCore(_cfg.LastXrayMode);
            }
        }
        private void RestartCore(string targetMode)
        {
            Task.Run(() => _xray.StopCore());

            _restartMode = targetMode;

            _bootstrap.ScheduleXrayRestart(OnXrayRestartTick);
        }
        private async void OnXrayRestartTick()
        {
            string targetMode = _restartMode;

            if (!await Task.Run(() => _xray.StartCore(targetMode))) return;

            ProxyService.SetSystemProxy(targetMode == XraySupervisor.ProxyMode);

            if (_state.IsConnected)
            {
                await _updateLanIp();
                _refreshPorts();

                CancellationTokens.CancelAndDispose(ref _vpn.PingCts);
                _vpn.PingCts = new CancellationTokenSource();
                var pToken = _vpn.PingCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(PingAfterRestart, pToken).ConfigureAwait(false);
                        if (!pToken.IsCancellationRequested)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (!pToken.IsCancellationRequested) _startGeoPing();
                            });
                        }
                    }
                    catch { }
                });
            }
        }
    }
}
