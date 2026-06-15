using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services
{
    public static class XrayConfigWriter
    {
        public static bool Write(AppConfig config, string xrayDir)
        {
            var rules = new List<object>
            {
                new { type = "field", ip = new[] { "127.0.0.0/8", "::1", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" }, outboundTag = "direct" },
                new { type = "field", domain = new[] { "domain:get.geojs.io" }, outboundTag = "proxy" }
            };

            var blockDomains = new List<string>();
            if (config.EnableAdBlock)
            {
                blockDomains.AddRange(new[] { "geosite:category-ads-all", "domain:analytics.google.com", "domain:google-analytics.com" });
            }
            if (config.EnableDirect && !string.IsNullOrWhiteSpace(config.LastBlockSplit))
            {
                foreach (var d in config.LastBlockSplit.Split(','))
                {
                    var t = d.Trim();
                    if (!string.IsNullOrEmpty(t)) blockDomains.Add($"domain:{t}");
                }
            }
            if (blockDomains.Count > 0)
                rules.Add(new { type = "field", domain = blockDomains.ToArray(), outboundTag = "block" });

            if (config.EnableDirect && config.LastXrayMode != "VPN Mode" && !string.IsNullOrWhiteSpace(config.LastManualSplit))
            {
                var domains = new List<string>();
                var ips = new List<string>();
                foreach (var item in config.LastManualSplit.Split(','))
                {
                    var t = item.Trim();
                    if (string.IsNullOrEmpty(t)) continue;
                    if (t.Any(char.IsLetter)) domains.Add($"domain:{t}");
                    else ips.Add(t);
                }
                string targetTag = config.SplitTunnelMode == "INCLUSIVE" ? "proxy" : "direct";
                if (domains.Count > 0) rules.Add(new { type = "field", domain = domains.ToArray(), outboundTag = targetTag });
                if (ips.Count > 0) rules.Add(new { type = "field", ip = ips.ToArray(), outboundTag = targetTag });
            }

            string defaultTag = (config.EnableDirect && config.SplitTunnelMode == "INCLUSIVE" && config.LastXrayMode != "VPN Mode") ? "direct" : "proxy";
            rules.Add(new { type = "field", network = "tcp,udp", outboundTag = defaultTag });

            var inbounds = new object[]
            {
                new { listen = config.AllowLanConnections ? "0.0.0.0" : "127.0.0.1", port = 10818, protocol = "mixed", tag = "mixed-in",
                      settings = new { udp = true },
                      sniffing = new { enabled = true, destOverride = new[] { "http", "tls", "quic", "fakedns" } } },
                new { listen = "127.0.0.1", port = 10899, protocol = "dokodemo-door", tag = "api",
                      settings = new { address = "127.0.0.1" } }
            };

            var outbounds = new List<object>();

            // V2ray chain
            if (config.EnableV2rayChain && !string.IsNullOrWhiteSpace(config.V2rayChainJson))
            {
                try
                {
                    var v2p = JObject.Parse(config.V2rayChainJson);
                    JObject? v2ob;

                    if (v2p["outbounds"] is JArray obArr)
                    {
                        v2ob = obArr.OfType<JObject>()
                            .FirstOrDefault(o => o["protocol"]?.ToString() != "freedom" && o["protocol"]?.ToString() != "blackhole");
                    }
                    else
                    {
                        v2ob = v2p;
                    }

                    if (v2ob != null)
                    {
                        v2ob["tag"] = "proxy";
                        // Allow insecure TLS
                        var tlsSettings = v2ob.SelectToken("streamSettings.tlsSettings");
                        if (tlsSettings is JObject tlsObj)
                        {
                            if (tlsObj["allowInsecure"] == null)
                                tlsObj.Add("allowInsecure", true);
                            else
                                tlsObj["allowInsecure"] = true;
                        }
                        // Chain through Tor
                        if (v2ob["proxySettings"] == null)
                            v2ob.Add("proxySettings", JObject.FromObject(new { tag = "torProxy" }));
                        else
                            v2ob["proxySettings"] = JObject.FromObject(new { tag = "torProxy" });

                        outbounds.Add(v2ob);
                        outbounds.Add(new { tag = "torProxy", protocol = "socks", settings = new { servers = new[] { new { address = "127.0.0.1", port = 10800 } } } });
                    }
                    else
                    {
                        outbounds.Add(new { tag = "proxy", protocol = "socks", settings = new { servers = new[] { new { address = "127.0.0.1", port = 10800 } } } });
                    }
                }
                catch
                {
                    outbounds.Add(new { tag = "proxy", protocol = "socks", settings = new { servers = new[] { new { address = "127.0.0.1", port = 10800 } } } });
                }
            }
            else
            {
                outbounds.Add(new { tag = "proxy", protocol = "socks", settings = new { servers = new[] { new { address = "127.0.0.1", port = 10800 } } } });
            }

            if (config.EnableAdBlock || (config.EnableDirect && !string.IsNullOrWhiteSpace(config.LastBlockSplit)))
                outbounds.Add(new { tag = "block", protocol = "blackhole", settings = new { } });

            outbounds.Add(new { tag = "direct", protocol = "freedom", settings = new { } });

            var allRules = new List<object>
            {
                new { type = "field", inboundTag = new[] { "api" }, outboundTag = "api" }
            };
            allRules.AddRange(rules);

            var cfg = new Dictionary<string, object>
            {
                ["log"] = new { logLevel = "info", access = "access.log", error = "error.log" },
                ["stats"] = new { },
                ["api"] = new { tag = "api", services = new[] { "StatsService" } },
                ["policy"] = new { system = new { statsInboundUplink = true, statsInboundDownlink = true } },
                ["inbounds"] = inbounds,
                ["outbounds"] = outbounds.ToArray(),
                ["routing"] = new { domainStrategy = "AsIs", rules = allRules.ToArray() }
            };

            if (config.EnableUpstreamDoh && !string.IsNullOrWhiteSpace(config.UpstreamDohUrl))
                cfg["dns"] = new { servers = new[] { config.UpstreamDohUrl } };

            try
            {
                var json = JsonConvert.SerializeObject(cfg, Formatting.Indented);
                File.WriteAllText(Path.Combine(xrayDir, "config.json"), json);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to write Xray config. Check disk space and permissions.\n\n{ex.Message}", "Config Error");
                return false;
            }
        }
    }

    public static class SingboxConfigWriter
    {
        public static bool Write(AppConfig config, string sbDir)
        {
            var currentExe = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "").ToLower();

            var systemBypassApps = new List<string>
            {
                currentExe, "tor.exe", "tor", "haproxy.exe", "haproxy",
                "lyrebird.exe", "lyrebird", "xray.exe", "xray",
                "sing-box.exe", "sing-box", "cmd.exe", "conhost.exe",
                "powershell.exe", "pwsh.exe"
            };

            var userApps = new List<string>();

            if (config.EnableDirect && !string.IsNullOrWhiteSpace(config.LastAppSplit))
            {
                foreach (var app in config.LastAppSplit.Split(','))
                {
                    var a = app.Trim();
                    if (string.IsNullOrEmpty(a)) continue;
                    var appExe = a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? a : a + ".exe";
                    var appBase = appExe.Replace(".exe", "");
                    if (!userApps.Contains(appExe.ToLower())) userApps.Add(appExe.ToLower());
                    if (!userApps.Contains(appBase.ToLower())) userApps.Add(appBase.ToLower());
                }
            }

            var sbRules = new List<object>
            {
                new { action = "sniff" },
                new { protocol = "quic", action = "reject", method = "default" },
                new { protocol = "dns", action = "hijack-dns" },
                new { port = new[] { 53 }, network = "udp", action = "hijack-dns" },
                new { port = new[] { 53 }, network = "tcp", action = "hijack-dns" },
                new { process_name = systemBypassApps.ToArray(), action = "route", outbound = "direct" }
            };

            if (userApps.Count > 0)
            {
                string targetOutbound = config.SplitTunnelMode == "INCLUSIVE" ? "proxy" : "direct";
                sbRules.Add(new { process_name = userApps.ToArray(), action = "route", outbound = targetOutbound });
            }

            sbRules.Add(new { network = "udp", port = new[] { 3478, 5349 }, action = "route", outbound = "direct" });
            sbRules.Add(new { ip_is_private = true, action = "route", outbound = "direct" });

            var dnsServers = new List<object>
            {
                new { tag = "dns_direct", type = "udp", server = "8.8.8.8" }
            };

            if (config.EnableUpstreamDoh && !string.IsNullOrWhiteSpace(config.UpstreamDohUrl))
            {
                if (config.UpstreamDohUrl.StartsWith("https://"))
                {
                    try
                    {
                        var u = new Uri(config.UpstreamDohUrl);
                        var dPath = u.AbsolutePath == "/" ? "/dns-query" : u.PathAndQuery;
                        dnsServers.Add(new { tag = "dns_proxy", type = "https", server = u.Host, path = dPath, detour = "proxy" });
                    }
                    catch
                    {
                        dnsServers.Add(new { tag = "dns_proxy", type = "tcp", server = "1.1.1.1", detour = "proxy" });
                    }
                }
                else
                {
                    dnsServers.Add(new { tag = "dns_proxy", type = "tcp", server = config.UpstreamDohUrl, detour = "proxy" });
                }
            }
            else
            {
                dnsServers.Add(new { tag = "dns_proxy", type = "https", server = "cloudflare-dns.com", path = "/dns-query", detour = "proxy" });
            }

            var dnsRules = new List<object>
            {
                new { domain_keyword = new[] { "stun", "cdn77", "datapacket" }, action = "route", server = "dns_direct" }
            };

            if (config.EnableDirect && config.SplitTunnelMode == "INCLUSIVE")
            {
                if (userApps.Count > 0)
                {
                    dnsRules.Add(new { process_name = userApps.ToArray(), action = "route", server = "dns_proxy" });
                }
                dnsRules.Add(new { action = "route", server = "dns_direct" });
            }
            else if (config.EnableDirect && config.SplitTunnelMode == "EXCLUSIVE")
            {
                if (userApps.Count > 0)
                {
                    dnsRules.Add(new { process_name = userApps.ToArray(), action = "route", server = "dns_direct" });
                }
                dnsRules.Add(new { action = "route", server = "dns_proxy" });
            }
            else
            {
                dnsRules.Add(new { action = "route", server = "dns_proxy" });
            }

            var sbConfig = new
            {
                log = new { level = "fatal" },
                dns = new
                {
                    servers = dnsServers.ToArray(),
                    rules = dnsRules.ToArray(),
                    strategy = "ipv4_only"
                },
                inbounds = new object[]
                {
                    new
                    {
                        type = "tun", tag = "tun-in",
                        interface_name = "singbox_tun",
                        address = new[] { "172.18.0.1/30" },
                        mtu = 9000, auto_route = true, strict_route = true,
                        stack = "mixed", endpoint_independent_nat = true
                    }
                },
                outbounds = new object[]
                {
                    new { type = "socks", tag = "proxy", server = "127.0.0.1", server_port = 10818 },
                    new { type = "direct", tag = "direct" }
                },
                route = new
                {
                    rules = sbRules.ToArray(),
                    final = config.EnableDirect && config.SplitTunnelMode == "INCLUSIVE" ? "direct" : "proxy",
                    default_domain_resolver = new { server = "dns_direct" },
                    auto_detect_interface = true,
                    find_process = true
                }
            };

            try
            {
                var json = JsonConvert.SerializeObject(sbConfig, Formatting.Indented);
                File.WriteAllText(Path.Combine(sbDir, "config.json"), json);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to write Sing-Box config.\n\n{ex.Message}", "Config Error");
                return false;
            }
        }
    }
}
