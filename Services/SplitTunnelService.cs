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
internal static class SplitTunnelService
{
    public const string DefaultAdapter = "default";
    private const string EntrySeparator = " - ";
    public static List<string> ListUsableAdapters(bool includeDefault)
    {
        var entries = new List<string>();
        if (includeDefault) entries.Add(DefaultAdapter);

        foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
            if (adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

            var ipv4 = adapter.GetIPProperties().UnicastAddresses.FirstOrDefault(
                a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipv4 != null && !string.IsNullOrWhiteSpace(ipv4.Address.ToString()))
                entries.Add(FormatAdapterEntry(adapter.Name, ipv4.Address.ToString()));
        }

        return entries;
    }
    public static string FormatAdapterEntry(string name, string ip) => $"{name}{EntrySeparator}{ip}";
    public static bool TryParseAdapterEntry(string? entry, out string name, out string ip)
    {
        name = string.Empty;
        ip = string.Empty;
        if (string.IsNullOrEmpty(entry)) return false;

        var parts = entry.Split(new[] { EntrySeparator }, StringSplitOptions.None);
        if (parts.Length < 2) return false;

        ip = parts[parts.Length - 1];
        name = string.Join(EntrySeparator, parts, 0, parts.Length - 1);
        return true;
    }
    public static int FindAdapterIndex(IEnumerable<string> entries, string name, string ip)
        => entries.ToList().IndexOf(FormatAdapterEntry(name, ip));
    public static bool IsAdapterUp(string? name) => FindUpAdapter(name) != null;
    public static System.Net.NetworkInformation.NetworkInterface? FindUpAdapter(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.Name == name && adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                return adapter;
        }

        return null;
    }
    public static bool HasAnySplitInput(AppConfig cfg)
        => !string.IsNullOrWhiteSpace(cfg.LastManualSplit)
        || !string.IsNullOrWhiteSpace(cfg.LastAppSplit)
        || !string.IsNullOrWhiteSpace(cfg.LastBlockSplit);
    public static bool RestartAppliesChange(string runningMode)
        => runningMode == XrayModes.ProxyMode || runningMode == XrayModes.ClearProxy;
}
