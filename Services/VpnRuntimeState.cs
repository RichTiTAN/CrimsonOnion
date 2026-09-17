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
public sealed class VpnRuntimeState
{
    public int?[] TorPids = new int?[8];

    public int? XrayDebugPid;
    public int? SbDebugPid;
    public int? AdapterXrayDebugPid;
    public int? XrayPid;
    public int? AdapterXrayPid;
    public int? SbPid;
    public System.Threading.CancellationTokenSource? UpdateCts;
    public System.Threading.CancellationTokenSource? StatsCts;
    public System.Threading.CancellationTokenSource? PingCts;
    public System.Threading.CancellationTokenSource? GeoCts;
    public long LastUpBytes;
    public long LastDnBytes;
    public System.DateTime LastPollTime = System.DateTime.MinValue;
    public int IsFetchingStatsInt;

}
