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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services
{
    public class DnsttProxyArgs
    {
        public string DnsArg { get; set; } = string.Empty;
        public string PubKey { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public int LocalPort { get; set; }
        public string Fingerprint { get; set; } = string.Empty;
    }

    public static class DnsttManager
    {
        private static ConcurrentDictionary<int, Process> _activeTunnels = new ConcurrentDictionary<int, Process>();

        public static (List<string> ModifiedBridges, List<DnsttProxyArgs> Proxies) ProcessDnsttBridges(string rawBridgeLines, int torInstanceId)
        {
            var modifiedBridges = new List<string>();
            var proxies = new List<DnsttProxyArgs>();

            if (string.IsNullOrWhiteSpace(rawBridgeLines))
                return (modifiedBridges, proxies);

            string lines = rawBridgeLines;
            lines = Regex.Replace(lines, @"\r\n|\n|\r", " ");
            lines = Regex.Replace(lines, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            lines = Regex.Replace(lines, @"\s*=\s*", "=");
            lines = Regex.Replace(lines, @"doh=https?://\s+", "doh=https://");
            lines = Regex.Replace(lines, @"(?=(?:Bridge\s+)?(?:dnstt|obfs4|meek|webtunnel|snowflake)\s)", "\n");

            int dnsttIndex = 0;

            foreach (var bl in lines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = bl.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("ClientTransportPlugin", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (trimmed.StartsWith("Bridge dnstt", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("dnstt ", StringComparison.OrdinalIgnoreCase))
                {
                    string doh = "", dot = "", pubkey = "", domain = "", fingerprint = "";
                    var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length >= 3 && !parts[2].Contains("="))
                    {
                        fingerprint = parts[2]; 
                    }

                    foreach (var p in parts)
                    {
                        if (p.StartsWith("doh=", StringComparison.OrdinalIgnoreCase)) doh = p.Substring(4);
                        else if (p.StartsWith("dot=", StringComparison.OrdinalIgnoreCase)) dot = p.Substring(4);
                        else if (p.StartsWith("pubkey=", StringComparison.OrdinalIgnoreCase)) pubkey = p.Substring(7);
                        else if (p.StartsWith("domain=", StringComparison.OrdinalIgnoreCase)) domain = p.Substring(7);
                    }

                    if ((!string.IsNullOrEmpty(doh) || !string.IsNullOrEmpty(dot)) && !string.IsNullOrEmpty(pubkey) && !string.IsNullOrEmpty(domain))
                    {
                        int port = 7000 + dnsttIndex;
                        string dnsArg = !string.IsNullOrEmpty(doh) ? $"-doh {doh}" : $"-dot {dot}";

                        proxies.Add(new DnsttProxyArgs
                        {
                            DnsArg = dnsArg,
                            PubKey = pubkey,
                            Domain = domain,
                            LocalPort = port,
                            Fingerprint = fingerprint
                        });

                        modifiedBridges.Add($"Bridge 127.0.0.1:{port} {fingerprint}");
                        dnsttIndex++;
                        continue;
                    }
                }

                modifiedBridges.Add(trimmed.StartsWith("Bridge ", StringComparison.OrdinalIgnoreCase) ? trimmed : $"Bridge {trimmed}");
            }

            return (modifiedBridges, proxies);
        }

        public static void StartTunnels(IEnumerable<DnsttProxyArgs> proxies, string workingDirectory, AppConfig config, Action<string>? debugLogger = null)
        {
            string exePath = Path.Combine(workingDirectory, "dnstt-client.exe");
            if (!File.Exists(exePath))
                return;

            string proxyUrl = "";

            if (config.EnableAdapterBinding && !string.IsNullOrWhiteSpace(config.SelectedAdapterIp))
            {
                proxyUrl = "socks5://127.0.0.1:10819";
            }
            else if (config.EnableOutboundProxy && !string.IsNullOrEmpty(config.OutboundProxyAddress) && !string.IsNullOrEmpty(config.OutboundProxyPort))
            {
                string scheme = config.OutboundProxyType == "HTTPS" ? "http" : "socks5";
                string auth = "";
                if (config.EnableOutboundAuth && !string.IsNullOrEmpty(config.OutboundProxyUser) && !string.IsNullOrEmpty(config.OutboundProxyPass))
                {
                    auth = $"{config.OutboundProxyUser}:{config.OutboundProxyPass}@";
                }
                proxyUrl = $"{scheme}://{auth}{config.OutboundProxyAddress}:{config.OutboundProxyPort}";
            }

            foreach (var proxy in proxies)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"{proxy.DnsArg} -pubkey {proxy.PubKey} {proxy.Domain} 127.0.0.1:{proxy.LocalPort}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = workingDirectory
                    };

                    if (!string.IsNullOrEmpty(proxyUrl))
                    {
                        psi.EnvironmentVariables["ALL_PROXY"] = proxyUrl;
                        psi.EnvironmentVariables["HTTP_PROXY"] = proxyUrl;
                        psi.EnvironmentVariables["HTTPS_PROXY"] = proxyUrl;
                        psi.EnvironmentVariables["all_proxy"] = proxyUrl;
                        psi.EnvironmentVariables["http_proxy"] = proxyUrl;
                        psi.EnvironmentVariables["https_proxy"] = proxyUrl;
                    }

                    if (config.DebugMode && debugLogger != null)
                    {
                        psi.RedirectStandardError = true;
                    }

                    var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                    if (config.DebugMode && debugLogger != null)
                    {
                        process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) debugLogger(e.Data); };
                    }

                    if (process.Start())
                    {
                        if (config.DebugMode && debugLogger != null)
                        {
                            process.BeginErrorReadLine();
                        }
                        _activeTunnels.TryAdd(process.Id, process);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to start dnstt-client.exe: {ex.Message}");
                }
            }
        }

        public static void StopAllTunnels()
        {
            foreach (var kvp in _activeTunnels)
            {
                try
                {
                    if (!kvp.Value.HasExited)
                        kvp.Value.Kill();
                }
                catch { /* Ignore errors during termination */ }
                finally
                {
                    try { kvp.Value.Dispose(); } catch { }
                }
            }
            _activeTunnels.Clear();

            try
            {
                var orphaned = Process.GetProcessesByName("dnstt-client");
                foreach (var proc in orphaned)
                {
                    try { proc.Kill(); } catch { }
                    finally { try { proc.Dispose(); } catch { } }
                }
            }
            catch { }
        }
    }
}
