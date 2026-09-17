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

namespace CrimsonOnion.Services;
internal sealed class BootstrapOrchestrator
{
    private static readonly TimeSpan AutoConnectInterval  = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ProgressPollInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan XrayBootInterval     = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan XrayRestartInterval  = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SessionClockInterval = TimeSpan.FromSeconds(1);

    private DispatcherTimer? _autoConnectTimer;
    private DispatcherTimer? _progressTimer;
    private DispatcherTimer? _xrayBootTimer;
    private DispatcherTimer? _xrayRestartTimer;
    private DispatcherTimer? _sessionClockTimer;

    // -- Auto-connect --------------------------------------------------------
    internal void AutoConnect(Action connect)
    {
        StopAutoConnect();
        _autoConnectTimer = new DispatcherTimer { Interval = AutoConnectInterval };
        _autoConnectTimer.Tick += (s, e) => { StopAutoConnect(); connect(); };
        _autoConnectTimer.Start();
    }
    internal void StopAutoConnect()
    {
        _autoConnectTimer?.Stop();
        _autoConnectTimer = null;
    }

    // -- Tor progress poll ---------------------------------------------------
    internal bool IsProgressPolling => _progressTimer?.IsEnabled == true;
    internal void StartProgressPolling(Action tick)
    {
        StopProgressPolling();
        _progressTimer = new DispatcherTimer { Interval = ProgressPollInterval };
        _progressTimer.Tick += (s, e) => tick();
        _progressTimer.Start();
    }
    internal void StopProgressPolling()
    {
        _progressTimer?.Stop();
        _progressTimer = null;
    }

    // -- Xray boot -----------------------------------------------------------
    internal void BootXrayOnce(Func<Task> start)
    {
        StopXrayBoot();
        _xrayBootTimer = new DispatcherTimer { Interval = XrayBootInterval };
        _xrayBootTimer.Tick += async (s, e) =>
        {
            StopXrayBoot();
            await start();
        };
        _xrayBootTimer.Start();
    }
    internal void StopXrayBoot()
    {
        _xrayBootTimer?.Stop();
        _xrayBootTimer = null;
    }

    // -- Xray restart --------------------------------------------------------
    internal void ScheduleXrayRestart(Action restart)
    {
        StopXrayRestart();
        _xrayRestartTimer = new DispatcherTimer { Interval = XrayRestartInterval };
        _xrayRestartTimer.Tick += (s, e) => { StopXrayRestart(); restart(); };
        _xrayRestartTimer.Start();
    }
    internal void StopXrayRestart()
    {
        _xrayRestartTimer?.Stop();
        _xrayRestartTimer = null;
    }

    // -- Session clock -------------------------------------------------------
    internal void StartSessionClock(Action tick)
    {
        if (_sessionClockTimer == null)
        {
            _sessionClockTimer = new DispatcherTimer { Interval = SessionClockInterval };
            _sessionClockTimer.Tick += (s, e) => tick();
        }

        _sessionClockTimer.Stop();
        _sessionClockTimer.Start();
    }
    internal void StopSessionClock()
    {
        _sessionClockTimer?.Stop();
        _sessionClockTimer = null;
    }

    // -- Lifecycle -----------------------------------------------------------

    internal void CancelSequence()
    {
        StopProgressPolling();
        StopXrayBoot();
        StopSessionClock();
    }
    internal void StopAll()
    {
        CancelSequence();
        StopAutoConnect();
        StopXrayRestart();
    }
}
