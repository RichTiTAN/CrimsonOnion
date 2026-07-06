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
using System.Text;
using Newtonsoft.Json.Linq;
using System.Web;

namespace CrimsonOnion.Services
{
    public static class XrayLinkParser
    {
        public static bool TryParseLink(string link, out string jsonResult)
        {
            jsonResult = string.Empty;
            if (string.IsNullOrWhiteSpace(link)) return false;

            link = link.Trim();
            try
            {
                JObject outbound = new JObject();

                if (link.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                {
                    outbound = ParseVmess(link.Substring(8));
                }
                else if (link.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                {
                    outbound = ParseVless(link);
                }
                else if (link.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase))
                {
                    outbound = ParseTrojan(link);
                }
                else if (link.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
                {
                    outbound = ParseShadowsocks(link);
                }
                else
                {
                    return false;
                }

                if (outbound == null || outbound["protocol"] == null)
                    return false;

                var outboundsArray = new JArray { outbound };
                var root = new JObject { ["outbounds"] = outboundsArray };
                jsonResult = root.ToString(Newtonsoft.Json.Formatting.Indented);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string DecodeBase64(string b64)
        {
            b64 = b64.Trim().Replace("-", "+").Replace("_", "/");
            int mod = b64.Length % 4;
            if (mod > 0) b64 += new string('=', 4 - mod);
            return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }

        private static JObject ParseVmess(string b64)
        {
            string json = DecodeBase64(b64);
            var v = JObject.Parse(json);

            var outbound = new JObject
            {
                ["protocol"] = "vmess",
                ["settings"] = new JObject
                {
                    ["vnext"] = new JArray
                    {
                        new JObject
                        {
                            ["address"] = v["add"]?.ToString(),
                            ["port"] = int.TryParse(v["port"]?.ToString(), out int p) ? p : 443,
                            ["users"] = new JArray
                            {
                                new JObject
                                {
                                    ["id"] = v["id"]?.ToString(),
                                    ["alterId"] = int.TryParse(v["aid"]?.ToString(), out int aid) ? aid : 0,
                                    ["security"] = string.IsNullOrEmpty(v["scy"]?.ToString()) ? "auto" : v["scy"]?.ToString()
                                }
                            }
                        }
                    }
                }
            };

            AddStreamSettings(outbound, v["net"]?.ToString(), v["tls"]?.ToString(), v["sni"]?.ToString(), v["alpn"]?.ToString(), v["host"]?.ToString(), v["path"]?.ToString(), v["fp"]?.ToString(), v["type"]?.ToString());

            return outbound;
        }

        private static JObject ParseVless(string link)
        {
            var uri = new Uri(link);
            var query = HttpUtility.ParseQueryString(uri.Query);

            var outbound = new JObject
            {
                ["protocol"] = "vless",
                ["settings"] = new JObject
                {
                    ["vnext"] = new JArray
                    {
                        new JObject
                        {
                            ["address"] = uri.IdnHost,
                            ["port"] = uri.Port,
                            ["users"] = new JArray
                            {
                                new JObject
                                {
                                    ["id"] = uri.UserInfo,
                                    ["encryption"] = query["encryption"] ?? "none"
                                }
                            }
                        }
                    }
                }
            };

            if (!string.IsNullOrEmpty(query["flow"]))
            {
                outbound["settings"]!["vnext"]![0]!["users"]![0]!["flow"] = query["flow"];
            }

            AddStreamSettings(outbound, query["type"], query["security"]?.ToLowerInvariant(), query["sni"], query["alpn"], query["host"], query["path"], query["fp"], query["headerType"], query["pbk"], query["sid"], query["spx"]);
            return outbound;
        }

        private static JObject ParseTrojan(string link)
        {
            var uri = new Uri(link);
            var query = HttpUtility.ParseQueryString(uri.Query);

            var outbound = new JObject
            {
                ["protocol"] = "trojan",
                ["settings"] = new JObject
                {
                    ["servers"] = new JArray
                    {
                        new JObject
                        {
                            ["address"] = uri.IdnHost,
                            ["port"] = uri.Port,
                            ["password"] = uri.UserInfo
                        }
                    }
                }
            };

            AddStreamSettings(outbound, query["type"], query["security"]?.ToLowerInvariant(), query["sni"], query["alpn"], query["host"], query["path"], query["fp"], query["headerType"], query["pbk"], query["sid"], query["spx"]);
            return outbound;
        }

        private static JObject ParseShadowsocks(string link)
        {
            string payload = link.Substring(5);
            string methodPass = "";
            string hostPort = "";

            int hashIdx = payload.IndexOf("#");
            if (hashIdx >= 0) payload = payload.Substring(0, hashIdx);

            if (payload.Contains("@"))
            {
                string[] parts = payload.Split(new[] { '@' }, 2);
                methodPass = DecodeBase64(parts[0]);
                hostPort = parts[1];
            }
            else
            {
                string decoded = DecodeBase64(payload);
                if (decoded.Contains("@"))
                {
                    string[] parts = decoded.Split(new[] { '@' }, 2);
                    methodPass = parts[0];
                    hostPort = parts[1];
                }
            }

            string[] mpParts = methodPass.Split(new[] { ':' }, 2);
            string[] hpParts = hostPort.Split(new[] { ':' }, 2);

            var outbound = new JObject
            {
                ["protocol"] = "shadowsocks",
                ["settings"] = new JObject
                {
                    ["servers"] = new JArray
                    {
                        new JObject
                        {
                            ["address"] = hpParts[0],
                            ["port"] = int.Parse(hpParts[1]),
                            ["method"] = mpParts[0],
                            ["password"] = mpParts[1]
                        }
                    }
                }
            };

            return outbound;
        }

        private static void AddStreamSettings(JObject outbound, string? net, string? tls, string? sni, string? alpn, string? host, string? path, string? fp, string? headerType, string? pbk = null, string? sid = null, string? spx = null)
        {
            var stream = new JObject();

            if (!string.IsNullOrEmpty(net)) stream["network"] = net;
            if (!string.IsNullOrEmpty(tls)) stream["security"] = tls;

            if (tls == "tls" || tls == "reality")
            {
                var tlsObj = new JObject();
                if (!string.IsNullOrEmpty(sni)) tlsObj["serverName"] = sni;
                if (!string.IsNullOrEmpty(fp)) tlsObj["fingerprint"] = fp;
                if (!string.IsNullOrEmpty(alpn)) tlsObj["alpn"] = new JArray(alpn.Split(','));
                
                if (tls == "reality")
                {
                    if (!string.IsNullOrEmpty(pbk)) tlsObj["publicKey"] = pbk;
                    if (!string.IsNullOrEmpty(sid)) tlsObj["shortId"] = sid;
                    if (!string.IsNullOrEmpty(spx)) tlsObj["spiderX"] = spx;
                }
                
                stream[tls + "Settings"] = tlsObj;
            }

            if (net == "ws")
            {
                var wsObj = new JObject();
                if (!string.IsNullOrEmpty(path)) wsObj["path"] = path;
                if (!string.IsNullOrEmpty(host)) wsObj["headers"] = new JObject { ["Host"] = host };
                stream["wsSettings"] = wsObj;
            }
            else if (net == "tcp")
            {
                if (headerType == "http")
                {
                    var tcpObj = new JObject
                    {
                        ["header"] = new JObject
                        {
                            ["type"] = "http",
                            ["request"] = new JObject
                            {
                                ["path"] = new JArray(string.IsNullOrEmpty(path) ? "/" : path)
                            }
                        }
                    };
                    if (!string.IsNullOrEmpty(host))
                        tcpObj["header"]!["request"]!["headers"] = new JObject { ["Host"] = new JArray(host.Split(',')) };
                    stream["tcpSettings"] = tcpObj;
                }
            }
            else if (net == "grpc")
            {
                var grpcObj = new JObject();
                if (!string.IsNullOrEmpty(path)) grpcObj["serviceName"] = path;
                stream["grpcSettings"] = grpcObj;
            }

            if (stream.Count > 0)
            {
                outbound["streamSettings"] = stream;
            }
        }
    }
}



