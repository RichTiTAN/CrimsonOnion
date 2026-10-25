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

using System.Net.NetworkInformation;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services
{
    internal static class LoadBalanceAdapters
    {
        internal const int MinSlots = 2;
        internal const int MaxSlots = 4;

        internal const int ProxyPortBase = 10900;

        internal const int SingleAdapterProxyPort = 10819;

        internal static List<string> ActiveEntries(AppConfig? cfg)
        {
            var entries = new List<string>();
            if (cfg == null) return entries;

            foreach (string pick in cfg.LoadBalanceAdapters ?? new List<string>())
            {
                string entry = (pick ?? "").Trim();
                if (entry.Length == 0) continue;

                string ip = IpOf(entry);
                if (ip.Length == 0 || !IsAddressLive(ip)) continue;
                if (entries.Any(existing => string.Equals(IpOf(existing), ip, StringComparison.OrdinalIgnoreCase))) continue;

                entries.Add(entry);
            }

            return entries;
        }

        internal static List<string> ActiveIps(AppConfig? cfg)
            => ActiveEntries(cfg).Select(IpOf).Where(ip => ip.Length > 0).ToList();

        internal static bool IsUsable(AppConfig? cfg)
            => cfg is { EnableLoadBalanceAdapters: true } && ActiveIps(cfg).Count >= MinSlots;

        internal static int IndexForTor(AppConfig? cfg, int torIndex)
        {
            int count = ActiveIps(cfg).Count;
            if (count == 0) return -1;

            int slot = torIndex < 1 ? 1 : torIndex;
            return (slot - 1) % count;
        }

        internal static int ProxyPortForTor(AppConfig? cfg, int torIndex)
        {
            int index = IndexForTor(cfg, torIndex);
            return ProxyPortBase + (index < 0 ? 0 : index);
        }

        internal static List<string> StaleEntries(AppConfig? cfg)
        {
            var stale = new List<string>();
            if (cfg == null) return stale;

            foreach (string pick in cfg.LoadBalanceAdapters ?? new List<string>())
            {
                string entry = (pick ?? "").Trim();
                if (entry.Length == 0) continue;

                string ip = IpOf(entry);
                if (ip.Length == 0 || !IsAddressLive(ip)) stale.Add(entry);
            }

            return stale;
        }

        internal static string Describe(IEnumerable<string>? picks, int engines)
        {
            var adapters = new List<string>();
            foreach (string pick in picks ?? Enumerable.Empty<string>())
            {
                string entry = (pick ?? "").Trim();
                if (entry.Length == 0) continue;

                string ip = IpOf(entry);
                if (ip.Length == 0) continue;
                if (adapters.Any(existing => string.Equals(IpOf(existing), ip, StringComparison.OrdinalIgnoreCase))) continue;

                string name = NameOf(entry);
                adapters.Add(name.Length > 0 ? name : ip);
            }

            if (adapters.Count == 0 || engines < 1) return "";

            var parts = new List<string>();
            int shown = Math.Min(engines, 16);
            for (int slot = 1; slot <= shown; slot++)
                parts.Add($"TOR {slot:00} → {adapters[(slot - 1) % adapters.Count]}");

            return string.Join("  ·  ", parts);
        }

        internal static string NameOf(string? entry)
            => SplitTunnelService.TryParseAdapterEntry(entry, out string name, out _) ? name : "";

        internal static string IpOf(string? entry)
        {
            string text = (entry ?? "").Trim();
            if (text.Length == 0) return "";

            if (SplitTunnelService.TryParseAdapterEntry(text, out _, out string ip) && ip.Length > 0) return ip;
            return System.Net.IPAddress.TryParse(text, out _) ? text : "";
        }

        internal static bool IsAddressLive(string? ip)
        {
            string text = (ip ?? "").Trim();
            if (text.Length == 0) return false;
            if (!System.Net.IPAddress.TryParse(text, out var wanted)) return false;

            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.Equals(wanted)) return true;
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log(ex);
            }

            return false;
        }
    }
}
