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

using CrimsonOnion.Models;

namespace CrimsonOnion.Services;
internal enum TorSlotTone
{
    Idle,
    Warming,
    Ready,
    Error,
}
internal enum EngineCountOutcome
{
    Ignored,
    ReconnectRequired,
    Shrunk,
    Grown,
}
internal sealed class TorFarmService
{
    internal const int SlotCount = TorLauncherService.MaxTors;
    internal const int ProgressUnset = -1;
    internal const int ProgressErrored = -2;
    private const string TorrcFileName = "torrc";
    private const string TorExeRelativePath = @"Data\TorBin\tor.exe";
    private static readonly TimeSpan DefaultStaggerInterval = TimeSpan.FromMilliseconds(1500);

    private readonly AppState _state;
    private readonly VpnRuntimeState _vpn;
    private readonly AppConfig _cfg;
    private readonly Func<int> _activeEngines;
    private readonly Func<string, string, string, int?> _startTor;
    private readonly Func<string, string, string, string, int?> _startTorDebug;
    private readonly Action<int, string> _attachControlClient;
    private readonly Action<int, IReadOnlyList<DnsttProxyArgs>>? _startTunnels;
    private readonly Func<bool> _isBootstrapping;
    private readonly TimeSpan _staggerInterval;

    private readonly List<int> _launchQueue = new();
    private bool _pumping;
    internal event Action<int>? SlotChanged;
    internal TorFarmService(
        AppState state,
        VpnRuntimeState vpn,
        AppConfig cfg,
        Func<int> activeEngines,
        Func<string, string, string, int?> startTor,
        Func<string, string, string, string, int?> startTorDebug,
        Action<int, string> attachControlClient,
        Func<bool> isBootstrapping,
        Action<int, IReadOnlyList<DnsttProxyArgs>>? startTunnels = null,
        TimeSpan? staggerInterval = null)
    {
        _state               = state;
        _vpn                 = vpn;
        _cfg                 = cfg;
        _activeEngines       = activeEngines;
        _startTor            = startTor;
        _startTorDebug       = startTorDebug;
        _attachControlClient = attachControlClient;
        _isBootstrapping     = isBootstrapping;
        _startTunnels        = startTunnels;
        _staggerInterval     = staggerInterval ?? DefaultStaggerInterval;
    }
    internal string SelectedBridge { get; set; } = BridgeNames.Direct;
    internal int SelectedCount { get; set; } = 6;
    internal int PendingLaunchCount
    {
        get { lock (_launchQueue) return _launchQueue.Count; }
    }
    internal TimeSpan SlotLaunchInterval => _staggerInterval;
    internal bool IsLaunching => _pumping;
    internal bool CanLaunch => File.Exists(Path.Combine(_cfg.BaseDir, TorExeRelativePath));

    // -- What a slot looks like ----------------------------------------------
    internal (string Text, TorSlotTone Tone, double Opacity) Describe(int slot)
    {
        if (!TorLauncherService.IsValidIndex(slot)) return (string.Empty, TorSlotTone.Idle, 1.0);

        string padded = slot.ToString().PadLeft(2, '0');
        int visible   = _activeEngines();

        if (!_state.IsEngineRunning)
            return Closed(padded, slot <= visible);

        if (slot > visible && slot > SelectedCount)
            return Label(padded, CrimsonOnion.Localization.AppStrings.Disabled, TorSlotTone.Idle, 0.5);
        if (slot > SelectedCount)
            return Label(padded, CrimsonOnion.Localization.AppStrings.TorStatusOffline, TorSlotTone.Idle, 0.5);
        if (IsQueued(slot))
            return Label(padded, CrimsonOnion.Localization.AppStrings.TorStatusWaiting, TorSlotTone.Idle, 0.5);

        int pct = _state.TorPcts[slot - 1];
        if (pct == ProgressErrored) return Label(padded, "ERR",  TorSlotTone.Error,   1.0);
        if (pct == 100)             return Label(padded, "100%", TorSlotTone.Ready,   1.0);
        if (pct >= 0)               return Label(padded, $"{pct}%", TorSlotTone.Warming, 1.0);

        return Label(padded, CrimsonOnion.Localization.AppStrings.TorStatusBooting, TorSlotTone.Idle, 1.0);
    }
    internal void RefreshSlots()
    {
        for (int slot = 1; slot <= SlotCount; slot++) SlotChanged?.Invoke(slot);
    }
    internal void ResetProgress()
    {
        for (int slot = 1; slot <= SlotCount; slot++) _state.TorPcts[slot - 1] = ProgressUnset;
    }
    internal void ClearStaleArtifacts()
    {
        for (int slot = 1; slot <= SlotCount; slot++) TorLauncherService.DeleteStaleArtifacts(SlotPath(slot));
    }
    internal void KillFleet()
    {
        for (int slot = 1; slot <= SlotCount; slot++) VpnEngineService.KillPidRef(ref _vpn.TorPids[slot - 1]);

        TorLauncherService.KillProcesses(_cfg.BaseDir);
    }

    // -- Reports from the control clients ------------------------------------
    internal void NoteProgress(int slot, int pct)
    {
        if (!IsKnownSlot(slot)) return;
        if (_state.TorPcts[slot - 1] == 100) return;

        _state.TorPcts[slot - 1] = Math.Max(_state.TorPcts[slot - 1], pct);
        SlotChanged?.Invoke(slot);
    }
    internal void NoteDropped(int slot)
    {
        if (!IsKnownSlot(slot)) return;
        if (_state.TorPcts[slot - 1] == 100) return;

        _state.TorPcts[slot - 1] = ProgressErrored;
        SlotChanged?.Invoke(slot);
    }
    internal (int BestPercent, bool AnyReady) SummarizeBoot()
    {
        int best = ProgressUnset;
        bool anyReady = false;

        for (int slot = 1; slot <= SelectedCount; slot++)
        {
            int pct = _state.TorPcts[slot - 1];
            if (pct > best) best = pct;
            if (pct == 100) anyReady = true;
        }

        return (best, anyReady);
    }

    // -- Growing and shrinking the fleet -------------------------------------
    internal EngineCountOutcome ApplyEngineCount(int newCount)
    {
        if (_state.IsConnected) return EngineCountOutcome.ReconnectRequired;
        if (!_state.IsEngineRunning) return EngineCountOutcome.Ignored;

        int current = SelectedCount;
        if (newCount == current) return EngineCountOutcome.Ignored;

        SelectedCount = newCount;
        return newCount < current ? ShrinkTo(newCount) : GrowTo(current, newCount);
    }

    private EngineCountOutcome ShrinkTo(int newCount)
    {
        for (int slot = newCount + 1; slot <= SlotCount; slot++)
        {
            VpnEngineService.KillPidRef(ref _vpn.TorPids[slot - 1]);
            ForgetLaunch(slot);
            SlotChanged?.Invoke(slot);
        }

        return EngineCountOutcome.Shrunk;
    }

    private EngineCountOutcome GrowTo(int current, int newCount)
    {
        bool booting = _isBootstrapping();

        for (int slot = current + 1; slot <= newCount; slot++)
        {
            if (TorLauncherService.IsValidIndex(slot)) _state.TorPcts[slot - 1] = ProgressUnset;

            TorLauncherService.DeleteStaleArtifacts(SlotPath(slot));
            if (booting) QueueLaunch(slot);

            SlotChanged?.Invoke(slot);
        }

        if (booting && !IsLaunching && PendingLaunchCount > 0) StartLaunchPump();

        return EngineCountOutcome.Grown;
    }

    // -- The staggered launch queue ------------------------------------------
    internal void QueueLaunch(int slot)
    {
        lock (_launchQueue) _launchQueue.Add(slot);
    }
    internal void ForgetLaunch(int slot)
    {
        lock (_launchQueue) _launchQueue.Remove(slot);
    }
    internal bool TryTakeLaunch(out int slot)
    {
        lock (_launchQueue)
        {
            if (_launchQueue.Count == 0)
            {
                slot = 0;
                return false;
            }

            slot = _launchQueue[0];
            _launchQueue.RemoveAt(0);
            return true;
        }
    }
    internal void StopLaunching()
    {
        _pumping = false;
        lock (_launchQueue) _launchQueue.Clear();
    }
    private void StartLaunchPump()
    {
        if (_pumping) return;
        _pumping = true;

        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await Task.Delay(_staggerInterval).ConfigureAwait(false);
                    if (_state.AbortBoot || !_state.IsEngineRunning) break;
                    if (!TryTakeLaunch(out int slot)) break;

                    await LaunchSlotAsync(slot).ConfigureAwait(false);
                }
            }
            finally { StopLaunching(); }
        });
    }

    private bool IsQueued(int slot)
    {
        lock (_launchQueue) return _launchQueue.Contains(slot);
    }

    // -- Starting one slot ---------------------------------------------------
    internal async Task<bool> LaunchSlotAsync(int slot)
    {
        if (!IsKnownSlot(slot)) return false;

        string torExe = Path.Combine(_cfg.BaseDir, TorExeRelativePath);
        if (!File.Exists(torExe)) return false;

        string slotPath = SlotPath(slot);
        TorLauncherService.DeleteStaleArtifacts(slotPath);
        if (!Directory.Exists(slotPath)) Directory.CreateDirectory(slotPath);

        var lines = TorrcBuilder.BuildTorrcConfig(
            TorrcFileName, SelectedBridge, _cfg.LastConfig, slotPath, _cfg, out var dnsttProxies);

        await File.WriteAllLinesAsync(Path.Combine(slotPath, TorrcFileName), lines);

        int? pid = await Task.Run(() =>
        {
            if (slot == 1 && dnsttProxies is { Count: > 0 }) _startTunnels?.Invoke(slot, dnsttProxies);

            return _cfg.DebugMode
                ? _startTorDebug(torExe, $"-f {TorrcFileName}", slotPath, $"Tor{slot}")
                : _startTor(torExe, $"-f {TorrcFileName}", slotPath);
        });

        if (!pid.HasValue) return false;

        _vpn.TorPids[slot - 1] = pid.Value;
        _attachControlClient(slot, slotPath);
        SlotChanged?.Invoke(slot);
        return true;
    }

    // -- Internals -----------------------------------------------------------
    private static (string, TorSlotTone, double) Closed(string padded, bool offline)
        => Label(padded, offline
            ? CrimsonOnion.Localization.AppStrings.TorStatusOffline
            : CrimsonOnion.Localization.AppStrings.Disabled,
            TorSlotTone.Idle, 0.5);

    private static (string, TorSlotTone, double) Label(string padded, string status, TorSlotTone tone, double opacity)
        => ($"TOR {padded}: {status}", tone, opacity);

    private static bool IsKnownSlot(int slot) => TorLauncherService.IsValidIndex(slot);

    private string SlotPath(int slot) => Path.Combine(_cfg.BaseDir, $@"Data\Tors\Tor{slot}");
}
