using System.IO;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services
{
    public static class TorrcBuilder
    {
        public static readonly Dictionary<string, BridgeEntry> BridgeData = new()
        {
            ["meek_lite"] = new BridgeEntry
            {
                Plugin = "ClientTransportPlugin meek_lite,obfs2,obfs3,obfs4,scramblesuit,webtunnel exec %%LYREBIRD%%",
                Lines = new[]
                {
                    "Bridge meek_lite 192.0.2.20:80 url=https://1603026938.rsc.cdn77.org front=www.phpmyadmin.net utls=HelloRandomizedALPN"
                }
            },
            ["obfs4"] = new BridgeEntry
            {
                Plugin = "ClientTransportPlugin meek_lite,obfs2,obfs3,obfs4,scramblesuit,webtunnel exec %%LYREBIRD%%",
                Lines = new[]
                {
                    "Bridge obfs4 37.218.245.14:38224 D9A82D2F9C2F65A18407B1D2B764F130847F8B5D cert=bjRaMrr1BRiAW8IE9U5z27fQaYgOhX1UCmOpg2pFpoMvo6ZgQMzLsaTzzQNTlm7hNcb+Sg iat-mode=0",
                    "Bridge obfs4 209.148.46.65:443 74FAD13168806246602538555B5521A0383A1875 cert=ssH+9rP8dG2NLDN2XuFw63hIO/9MNNinLmxQDpVa+7kTOa9/m+tGWT1SmSYpQ9uTBGa6Hw iat-mode=0",
                    "Bridge obfs4 146.57.248.225:22 10A6CD36A537FCE513A322361547444B393989F0 cert=K1gDtDAIcUfeLqbstggjIw2rtgIKqdIhUlHp82XRqNSq/mtAjp1BIC9vHKJ2FAEpGssTPw iat-mode=0",
                    "Bridge obfs4 45.145.95.6:27015 C5B7CD6946FF10C5B3E89691A7D3F2C122D2117C cert=TD7PbUO0/0k6xYHMPW3vJxICfkMZNdkRrb63Zhl5j9dW3iRGiCx0A7mPhe5T2EDzQ35+Zw iat-mode=0",
                    "Bridge obfs4 51.222.13.177:80 5EDAC3B810E12B01F6FD8050D2FD3E277B289A08 cert=2uplIpLQ0q9+0qMFrK5pkaYRDOe460LL9WHBvatgkuRr/SL31wBOEupaMMJ6koRE6Ld0ew iat-mode=1",
                    "Bridge obfs4 212.83.43.95:443 BFE712113A72899AD685764B211FACD30FF52C31 cert=ayq0XzCwhpdysn5o0EyDUbmSOx3X/oTEbzDMvczHOdBJKlvIdHHLJGkZARtT4dcBFArPPg iat-mode=1",
                    "Bridge obfs4 212.83.43.74:443 39562501228A4D5E27FCA4C0C81A01EE23AE3EE4 cert=PBwr+S8JTVZo6MPdHnkTwXJPILWADLqfMGoVvhZClMq/Urndyd42BwX9YFJHZnBB3H0XCw iat-mode=1"
                }
            },
            ["snowflake"] = new BridgeEntry
            {
                Plugin = "ClientTransportPlugin snowflake exec %%LYREBIRD%%",
                Lines = new[]
                {
                    "Bridge snowflake 192.0.2.3:80 2B280B23E1107BB62ABFC40DDCC8824814F80A72 fingerprint=2B280B23E1107BB62ABFC40DDCC8824814F80A72 url=https://1098762253.rsc.cdn77.org/ fronts=app.datapacket.com,www.datapacket.com ice=stun:stun.epygi.com:3478,stun:stun.uls.co.za:3478,stun:stun.voipgate.com:3478,stun:stun.mixvoip.com:3478,stun:stun.telnyx.com:3478,stun:stun.hot-chilli.net:3478,stun:stun.fitauto.ru:3478,stun:stun.m-online.net:3478 utls-imitate=hellorandomizedalpn",
                    "Bridge snowflake 192.0.2.4:80 8838024498816A039FCBBAB14E6F40A0843051FA fingerprint=8838024498816A039FCBBAB14E6F40A0843051FA url=https://1098762253.rsc.cdn77.org/ fronts=app.datapacket.com,www.datapacket.com ice=stun:stun.epygi.com:3478,stun:stun.uls.co.za:3478,stun:stun.voipgate.com:3478,stun:stun.mixvoip.com:3478,stun:stun.telnyx.com:3478,stun:stun.hot-chilli.net:3478,stun:stun.fitauto.ru:3478,stun:stun.m-online.net:3478 utls-imitate=hellorandomizedalpn"
                }
            }
        };

        public static List<string> BuildTorrcConfig(string torrcFile, string selBridge, string selConfig, string path, AppConfig config)
        {
            var fullPath = Path.Combine(path, torrcFile);
            var rawLines = File.Exists(fullPath) ? File.ReadAllLines(fullPath) : Array.Empty<string>();

            var cleanCfg = new List<string>();
            foreach (var line in rawLines)
            {
                if (line.StartsWith("# --- MANAGED BRIDGES ---", StringComparison.OrdinalIgnoreCase)) break;
                if (!line.TrimStart().StartsWith("UseBridges", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("ClientTransportPlugin", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("Bridge ", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("HTTPSProxy", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("Socks5Proxy", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("Socks5ProxyUsername", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("Socks5ProxyPassword", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("HTTPSProxyAuthenticator", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("Log notice file", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("MaxCircuitDirtiness", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("ExitNodes", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("StrictNodes", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("CircuitBuildTimeout", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("HardwareAccel", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("KeepalivePeriod", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("NewCircuitPeriod", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("FascistFirewall", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("ExcludeNodes", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("ExcludeExitNodes", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("DataDirectory", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("GeoIPFile", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("GeoIPv6File", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("ControlPort", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("SocksPort", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("CookieAuthentication", StringComparison.OrdinalIgnoreCase) &&
                    !line.TrimStart().StartsWith("# --- DYNAMIC ROUTING ---", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(line.Trim()))
                {
                    cleanCfg.Add(line.Trim());
                }
            }

            cleanCfg.Add("");
            cleanCfg.Add("# --- DYNAMIC ROUTING ---");
            cleanCfg.Add("DataDirectory ./Data");
            cleanCfg.Add("GeoIPFile ../../TorBin/geoip");
            cleanCfg.Add("GeoIPv6File ../../TorBin/geoip6");

            int torIndex = 1;
            var match = System.Text.RegularExpressions.Regex.Match(path, @"Tor(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int idx))
            {
                torIndex = idx;
            }
            else
            {
                throw new Exception($"Could not extract Tor index from path: {path}");
            }
            cleanCfg.Add($"SocksPort 127.0.0.1:{19050 + torIndex}");
            cleanCfg.Add($"ControlPort {20050 + torIndex}");
            cleanCfg.Add("CookieAuthentication 1");


            if (selConfig == "Optimized")
            {
                cleanCfg.Add("CircuitBuildTimeout 10");
                cleanCfg.Add("KeepalivePeriod 60");
                cleanCfg.Add("NewCircuitPeriod 120");
                cleanCfg.Add("HardwareAccel 1");
                cleanCfg.Add("ExitNodes {nl},{de},{it},{is},{fi},{au},{nz},{ch},{hk},{ae},{us}");
                cleanCfg.Add("StrictNodes 0");
            }
            else if (selConfig == "Expert")
            {
                cleanCfg.Add(config.ExpertHardwareAccel ? "HardwareAccel 1" : "HardwareAccel 0");
                if (config.ExpertFascistFirewall) cleanCfg.Add("FascistFirewall 1");
                cleanCfg.Add(config.ExpertStrictNodes ? "StrictNodes 1" : "StrictNodes 0");
                if (!string.IsNullOrWhiteSpace(config.ExpertCircuitBuildTimeout)) cleanCfg.Add($"CircuitBuildTimeout {config.ExpertCircuitBuildTimeout}");
                if (!string.IsNullOrWhiteSpace(config.ExpertKeepalivePeriod)) cleanCfg.Add($"KeepalivePeriod {config.ExpertKeepalivePeriod}");
                if (!string.IsNullOrWhiteSpace(config.ExpertNewCircuitPeriod)) cleanCfg.Add($"NewCircuitPeriod {config.ExpertNewCircuitPeriod}");
                if (!string.IsNullOrWhiteSpace(config.ExpertMaxCircuitDirtiness)) cleanCfg.Add($"MaxCircuitDirtiness {config.ExpertMaxCircuitDirtiness}");
                if (!string.IsNullOrWhiteSpace(config.ExpertNumEntryGuards)) cleanCfg.Add($"NumEntryGuards {config.ExpertNumEntryGuards}");
                if (!string.IsNullOrWhiteSpace(config.ExpertEntryNodes)) cleanCfg.Add($"EntryNodes {config.ExpertEntryNodes}");
                if (!string.IsNullOrWhiteSpace(config.ExpertExitNodes)) cleanCfg.Add($"ExitNodes {config.ExpertExitNodes}");
                if (!string.IsNullOrWhiteSpace(config.ExpertExcludeNodes)) cleanCfg.Add($"ExcludeNodes {config.ExpertExcludeNodes}");
                if (!string.IsNullOrWhiteSpace(config.ExpertExcludeExitNodes)) cleanCfg.Add($"ExcludeExitNodes {config.ExpertExcludeExitNodes}");
                if (!string.IsNullOrWhiteSpace(config.ExpertCustomTorrc))
                {
                    foreach (var l in config.ExpertCustomTorrc.Split('\n'))
                    {
                        var trimmed = l.Trim();
                        if (!string.IsNullOrEmpty(trimmed)) cleanCfg.Add(trimmed);
                    }
                }
            }
            else if (selConfig == "Custom")
            {
                if (!string.IsNullOrWhiteSpace(config.CustomExitCountry))
                {
                    cleanCfg.Add($"ExitNodes {{{config.CustomExitCountry}}}");
                    cleanCfg.Add("StrictNodes 1");
                }
            }
            else if (!string.IsNullOrWhiteSpace(selConfig))
            {
                cleanCfg.Add($"ExitNodes {{{selConfig.ToLower()}}}");
                cleanCfg.Add("StrictNodes 1");
            }
            else
            {
                cleanCfg.Add("ExitNodes {nl},{de},{it},{is},{fi},{au},{nz},{ch},{hk},{ae},{us}");
                cleanCfg.Add("StrictNodes 0");
            }

            cleanCfg.Add("");
            cleanCfg.Add("# --- MANAGED BRIDGES ---");
            cleanCfg.Add("Log notice file ./tor.log");

            if (config.EnableOutboundProxy && !string.IsNullOrEmpty(config.OutboundProxyAddress) && !string.IsNullOrEmpty(config.OutboundProxyPort))
            {
                if (config.OutboundProxyType == "SOCKS5")
                {
                    cleanCfg.Add($"Socks5Proxy {config.OutboundProxyAddress}:{config.OutboundProxyPort}");
                    if (config.EnableOutboundAuth && !string.IsNullOrEmpty(config.OutboundProxyUser) && !string.IsNullOrEmpty(config.OutboundProxyPass))
                    {
                        cleanCfg.Add($"Socks5ProxyUsername {config.OutboundProxyUser}");
                        cleanCfg.Add($"Socks5ProxyPassword {config.OutboundProxyPass}");
                    }
                }
                else if (config.OutboundProxyType == "HTTPS")
                {
                    cleanCfg.Add($"HTTPSProxy {config.OutboundProxyAddress}:{config.OutboundProxyPort}");
                    if (config.EnableOutboundAuth && !string.IsNullOrEmpty(config.OutboundProxyUser) && !string.IsNullOrEmpty(config.OutboundProxyPass))
                    {
                        cleanCfg.Add($"HTTPSProxyAuthenticator {config.OutboundProxyUser}:{config.OutboundProxyPass}");
                    }
                }
            }

            if (selBridge == "Custom")
            {
                if (!string.IsNullOrWhiteSpace(config.CustomBridgeLine))
                {
                    cleanCfg.Add("UseBridges 1");
                    cleanCfg.Add("ClientTransportPlugin meek_lite,obfs2,obfs3,obfs4,scramblesuit,webtunnel,snowflake exec ../../TorBin/lyrebird.exe");
                    foreach (var bl in config.CustomBridgeLine.Split('\n'))
                    {
                        var trimmed = bl.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("ClientTransportPlugin", StringComparison.OrdinalIgnoreCase))
                        {
                            cleanCfg.Add(trimmed.StartsWith("Bridge ", StringComparison.OrdinalIgnoreCase) ? trimmed : $"Bridge {trimmed}");
                        }
                    }
                }
                else
                {
                    cleanCfg.Add("UseBridges 0");
                }
            }
            else if (selBridge != "Direct")
            {
                if (BridgeData.TryGetValue(selBridge, out var b))
                {
                    cleanCfg.Add("UseBridges 1");
                    cleanCfg.Add(b.Plugin.Replace("%%LYREBIRD%%", "../../TorBin/lyrebird.exe"));
                    foreach (var bl in b.Lines) cleanCfg.Add(bl);
                }
                else
                {
                    cleanCfg.Add("UseBridges 0");
                }
            }
            else
            {
                cleanCfg.Add("UseBridges 0");
            }

            return cleanCfg;
        }
    }
}
