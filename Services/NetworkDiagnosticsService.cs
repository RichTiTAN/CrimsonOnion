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
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services;

public sealed class GeoResult
{
    public string Country       { get; init; } = string.Empty;
    public string CountryCode   { get; init; } = string.Empty;
    public string Continent     { get; init; } = string.Empty;
    public string ContinentCode { get; init; } = string.Empty;
    public long   PingMs        { get; init; }
}

public static class NetworkDiagnosticsService
{
    // ── HTTP clients ────────────────────────────────────────────────────────
    private static readonly HttpClient _geoPingClient = new HttpClient(
        new HttpClientHandler
        {
            Proxy    = new System.Net.WebProxy("http://127.0.0.1:10818"),
            UseProxy = true
        })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    // One plain client for the xray gRPC stats endpoint (no proxy needed).
    private static readonly HttpClient _grpcClient = new HttpClient(new HttpClientHandler())
    {
        DefaultRequestVersion  = new Version(2, 0),
        DefaultVersionPolicy   = HttpVersionPolicy.RequestVersionExact
    };

    // Pre-built body for the xray QueryStats gRPC call.
    private static readonly byte[] _grpcStatsQueryBody =
        { 0x00, 0x00, 0x00, 0x00, 0x02, 0x0A, 0x00 };

    // ── Public API ──────────────────────────────────────────────────────────

    public static async Task<GeoResult?> FetchGeoAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var json = await _geoPingClient.GetStringAsync(
                "https://get.geojs.io/v1/ip/geo.json", ct).ConfigureAwait(false);
            sw.Stop();

            var data          = Newtonsoft.Json.Linq.JObject.Parse(json);
            var continentCode = data["continent_code"]?.ToString() ?? string.Empty;
            var countryCode   = data["country_code"]?.ToString()   ?? string.Empty;
            var continent     = data["continent"]?.ToString()      ?? continentCode;
            var country       = data["country"]?.ToString()        ?? string.Empty;

            return new GeoResult
            {
                Country       = country,
                CountryCode   = countryCode,
                Continent     = continent,
                ContinentCode = continentCode,
                PingMs        = sw.ElapsedMilliseconds
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task<(long Up, long Dn)> FetchStatsAsync(CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "http://127.0.0.1:10899/xray.app.stats.command.StatsService/QueryStats")
            {
                Version       = new Version(2, 0),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            request.Content = new ByteArrayContent(_grpcStatsQueryBody);
            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/grpc");
            request.Headers.Add("TE", "trailers");

            using var cts      = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(1.5));

            using var response = await _grpcClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            var bytes          = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);

            return ParseGrpcStats(bytes);
        }
        catch
        {
            return (-1, -1);
        }
    }

    public static Task<string> GetLanIpAsync(AppConfig cfg)
    {
        return Task.Run(() =>
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                if (cfg.EnableAdapterBinding && !string.IsNullOrWhiteSpace(cfg.SelectedAdapterName))
                {
                    var specific = interfaces.FirstOrDefault(
                        ni => ni.Name == cfg.SelectedAdapterName);
                    if (specific != null)
                    {
                        var ip = specific.GetIPProperties().UnicastAddresses
                            .Where(ua => ua.Address.AddressFamily ==
                                         System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(ua => ua.Address.ToString())
                            .FirstOrDefault(s => !s.StartsWith("127.") &&
                                                 !s.StartsWith("169.254."));
                        if (!string.IsNullOrEmpty(ip)) return ip;
                    }
                }

                return interfaces
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(ua => ua.Address.AddressFamily ==
                                 System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(ua => ua.Address.ToString())
                    .FirstOrDefault(s => !s.StartsWith("127.") &&
                                        !s.StartsWith("169.254."))
                    ?? "UNKNOWN";
            }
            catch
            {
                return "UNKNOWN";
            }
        });
    }

    // ── Internals ───────────────────────────────────────────────────────────

    private static (long Up, long Dn) ParseGrpcStats(byte[] bytes)
    {
        long upVal = 0, dnVal = 0;
        int  pos   = 5;

        while (pos < bytes.Length)
        {
            if (bytes[pos] != 0x0A) break;
            pos++;

            int  statLen   = ReadVarint(bytes, ref pos);
            int  statEnd   = pos + statLen;
            bool isUplink  = false, isDownlink = false, isSocks = false;
            long value     = 0;

            while (pos < statEnd)
            {
                int tag = ReadVarint(bytes, ref pos);
                if (tag == 0x0A)
                {
                    int nameLen = ReadVarint(bytes, ref pos);
                    var span    = new ReadOnlySpan<byte>(bytes, pos, nameLen);
                    if (span.IndexOf("uplink"u8)              >= 0) isUplink   = true;
                    if (span.IndexOf("downlink"u8)            >= 0) isDownlink = true;
                    if (span.IndexOf("inbound>>>mixed-in"u8)  >= 0) isSocks    = true;
                    pos += nameLen;
                }
                else if (tag == 0x10)
                {
                    value = ReadVarint64(bytes, ref pos);
                }
                else
                {
                    int wireType = tag & 7;
                    if      (wireType == 0) ReadVarint64(bytes, ref pos);
                    else if (wireType == 1) pos += 8;
                    else if (wireType == 2) pos += ReadVarint(bytes, ref pos);
                    else if (wireType == 5) pos += 4;
                }
            }

            if (isSocks)
            {
                if (isUplink)   upVal += value;
                if (isDownlink) dnVal += value;
            }
        }

        return (upVal, dnVal);
    }

    internal static int ReadVarint(byte[] data, ref int p)
    {
        int result = 0, shift = 0;
        while (p < data.Length)
        {
            byte b = data[p++];
            if (shift < 32) result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        return result;
    }

    internal static long ReadVarint64(byte[] data, ref int p)
    {
        long result = 0; int shift = 0;
        while (p < data.Length)
        {
            byte b = data[p++];
            result |= (long)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        return result;
    }
}
