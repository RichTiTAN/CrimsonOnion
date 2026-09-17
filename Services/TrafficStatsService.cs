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

namespace CrimsonOnion.Services;
internal sealed class TrafficStatsService
{
    public const int HistorySize = 40;

    private const int PollIntervalMs = 1500;

    private readonly VpnRuntimeState _vpn;
    private readonly CrimsonOnion.Models.AppState _state;
    private readonly System.Collections.Generic.Queue<double> _up = new();
    private readonly System.Collections.Generic.Queue<double> _down = new();

    public TrafficStatsService(VpnRuntimeState vpn, CrimsonOnion.Models.AppState state)
    {
        _vpn = vpn;
        _state = state;
    }
    public System.Collections.Generic.Queue<double> Up => _up;
    public System.Collections.Generic.Queue<double> Down => _down;
    public event System.Action<string, string, string>? Sampled;
    public void Start()
    {
        Stop();
        _vpn.StatsCts = new System.Threading.CancellationTokenSource();
        var token = _vpn.StatsCts.Token;

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await System.Threading.Tasks.Task.Delay(PollIntervalMs, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                try { Poll(); } catch { }
            }
        }, token);
    }
    public void Stop()
    {
        CancellationTokens.CancelAndDispose(ref _vpn.StatsCts);
    }
    public void Clear()
    {
        _up.Clear();
        _down.Clear();
    }
    private void Poll()
    {
        if (!_state.IsConnected || System.Threading.Interlocked.CompareExchange(ref _vpn.IsFetchingStatsInt, 1, 0) != 0) return;

        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var (curUpBytes, curDnBytes) = await NetworkDiagnosticsService
                    .FetchStatsAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);

                if (curUpBytes < 0) return;

                if (curUpBytes > 0 && _vpn.LastUpBytes > 0)
                {
                    var diffUp = System.Math.Max(0, curUpBytes - _vpn.LastUpBytes);
                    var diffDn = System.Math.Max(0, curDnBytes - _vpn.LastDnBytes);
                    _state.SessionDataBytes += diffUp + diffDn;

                    _up.Enqueue(diffUp);
                    if (_up.Count > HistorySize) _up.Dequeue();

                    _down.Enqueue(diffDn);
                    if (_down.Count > HistorySize) _down.Dequeue();

                    var now = System.DateTime.UtcNow;
                    double elapsed = (now - _vpn.LastPollTime).TotalSeconds;
                    if (elapsed <= 0) elapsed = 1.0;

                    Sampled?.Invoke(
                        FormatRate(diffUp / elapsed),
                        FormatRate(diffDn / elapsed),
                        FormatTotal(_state.SessionDataBytes));
                }

                if (curUpBytes > 0) _vpn.LastUpBytes = curUpBytes;
                if (curDnBytes > 0) _vpn.LastDnBytes = curDnBytes;
                _vpn.LastPollTime = System.DateTime.UtcNow;
            }
            catch (System.Exception ex) { SimpleLogger.Log(ex); }
            finally { System.Threading.Interlocked.Exchange(ref _vpn.IsFetchingStatsInt, 0); }
        });
    }
    private static string FormatRate(double bytesPerSecond)
        => bytesPerSecond >= 1048576 ? $"{System.Math.Round(bytesPerSecond / 1048576.0, 2)} MB/s"
         : bytesPerSecond >= 1024 ? $"{System.Math.Round(bytesPerSecond / 1024.0, 1)} KB/s"
         : $"{System.Convert.ToInt32(bytesPerSecond)} B/s";
    private static string FormatTotal(long bytes)
        => bytes >= 1073741824 ? $"{System.Math.Round(bytes / 1073741824.0, 2)} GB"
         : bytes >= 1048576 ? $"{System.Math.Round(bytes / 1048576.0, 1)} MB"
         : $"{System.Math.Round(bytes / 1024.0, 1)} KB";
}
