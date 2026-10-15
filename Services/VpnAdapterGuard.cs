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
using System.Net.NetworkInformation;

namespace CrimsonOnion.Services;

internal static class VpnAdapterGuard
{
    private static readonly string[] NameMarkers = { "singbox", "sing-box", "wintun" };

    private static readonly string[] DescriptionMarkers =
    {
        TunProfile.DeviceDescriptionMarker, "singbox", "sing-box", "wintun"
    };

    internal static bool IsInUse()
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (Matches(adapter.Name, adapter.Description)) return true;
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.Log(ex);
        }
        return false;
    }

    internal static bool Matches(string name, string description)
    {
        name ??= "";
        description ??= "";
        if (name.Equals(TunProfile.InterfaceName, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var marker in NameMarkers)
        {
            if (name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        foreach (var marker in DescriptionMarkers)
        {
            if (description.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }
}
