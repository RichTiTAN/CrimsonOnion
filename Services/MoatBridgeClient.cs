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
internal enum MoatStatus
{
    Ok,
    Cancelled,
    Unreachable,
    NoChallenge
}
internal sealed class MoatBridgeClient : System.IDisposable
{
    private static readonly System.TimeSpan DefaultRequestTimeout = System.TimeSpan.FromSeconds(20);
    private static readonly string[] DefaultEndpoints =
    {
        "https://bridges.torproject.org/moat",
        "https://bridges2.torproject.org/moat",
        "https://tor.eff.org/moat"
    };

    private readonly string[] _endpoints;
    private readonly System.TimeSpan _requestTimeout;
    private readonly System.Func<System.Net.Http.HttpClient> _clientFactory;

    private string _transport = "";
    private string _challengeId = "";
    private string _challengeString = "";
    private int _index;
    private System.Net.Http.HttpClient? _httpClient;
    private System.Threading.CancellationTokenSource? _cts;
    public MoatBridgeClient(
        System.Func<System.Net.Http.HttpClient>? clientFactory = null,
        string[]? endpoints = null,
        System.TimeSpan? requestTimeout = null)
    {
        _clientFactory  = clientFactory ?? CreateProxiedClient;
        _endpoints      = endpoints ?? DefaultEndpoints;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }
    public bool IsFetching { get; private set; }
    public byte[]? ChallengeImage { get; private set; }
    public string? BridgeLines { get; private set; }
    public void Begin(string transport)
    {
        _transport     = transport;
        _index         = 0;
        ChallengeImage = null;
        BridgeLines    = null;

        var previous = _httpClient;
        System.Net.Http.HttpClient client;
        try
        {
            client = _clientFactory();
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
            client = new System.Net.Http.HttpClient();
        }
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");

        _httpClient = client;
        previous?.Dispose();
        IsFetching = true;
    }
    public void Cancel()
    {
        CancellationTokens.CancelAndDispose(ref _cts);

        var client  = _httpClient;
        _httpClient = null;
        IsFetching  = false;

        if (client == null) return;

        try
        {
            client.CancelPendingRequests();
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
        }

        try
        {
            client.Dispose();
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
        }
    }
    public void Dispose() => Cancel();
    private static System.Net.Http.HttpClient CreateProxiedClient()
    {
        try
        {
            var sysProxy = System.Net.WebRequest.GetSystemWebProxy();
            sysProxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            return new System.Net.Http.HttpClient(
                new System.Net.Http.HttpClientHandler { Proxy = sysProxy, UseProxy = true });
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
            return new System.Net.Http.HttpClient();
        }
    }
    private System.Threading.CancellationToken RenewRequestToken()
    {
        CancellationTokens.Forget(ref _cts);
        var source = new System.Threading.CancellationTokenSource(_requestTimeout);
        _cts = source;
        return source.Token;
    }
    public async System.Threading.Tasks.Task<MoatStatus> FetchChallengeAsync()
    {
        for (_index = 0; _index < _endpoints.Length; _index++)
        {
            if (!IsFetching) return MoatStatus.Cancelled;

            var url  = _endpoints[_index] + "/fetch";
            var body = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                data = new object[] { new { version = "0.1.0", type = "client-transports", supported = new[] { _transport } } }
            });

            try
            {
                var client = _httpClient;
                if (client == null) return MoatStatus.Cancelled;

                var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/vnd.api+json");
                using var response = await client.PostAsync(url, content, RenewRequestToken());
                var resultStr      = await response.Content.ReadAsStringAsync();

                if (!IsFetching) return MoatStatus.Cancelled;

                var res = Newtonsoft.Json.Linq.JObject.Parse(resultStr);
                if (res["data"] is Newtonsoft.Json.Linq.JArray dataArr
                    && dataArr.Count > 0
                    && dataArr[0] is Newtonsoft.Json.Linq.JObject d0
                    && d0["id"] != null && d0["image"] != null && d0["challenge"] != null)
                {
                    _challengeId     = d0["id"]!.ToString();
                    _challengeString = d0["challenge"]!.ToString();
                    ChallengeImage   = System.Convert.FromBase64String(d0["image"]!.ToString());
                    return MoatStatus.Ok;
                }
            }
            catch (System.Exception ex)
            {
                SimpleLogger.Log(ex);
                if (!IsFetching) return MoatStatus.Cancelled;
            }
        }

        return IsFetching ? MoatStatus.Unreachable : MoatStatus.Cancelled;
    }
    public async System.Threading.Tasks.Task<MoatStatus> SubmitSolutionAsync(string solution)
    {
        if (_index >= _endpoints.Length) return MoatStatus.NoChallenge;

        var url  = _endpoints[_index] + "/check";
        var body = Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            data = new object[]
            {
                new
                {
                    id = _challengeId, version = "0.1.0", type = "moat-solution",
                    transport = _transport, challenge = _challengeString,
                    solution = solution, qrcode = "false"
                }
            }
        });

        try
        {
            var client = _httpClient;
            if (client == null) return MoatStatus.Cancelled;

            var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/vnd.api+json");
            using var response = await client.PostAsync(url, content, RenewRequestToken());
            var resultStr      = await response.Content.ReadAsStringAsync();

            if (!IsFetching) return MoatStatus.Cancelled;

            var res     = Newtonsoft.Json.Linq.JObject.Parse(resultStr);
            var dataArr = res["data"] as Newtonsoft.Json.Linq.JArray;
            if (dataArr != null && dataArr.Count > 0
                && dataArr[0]["bridges"] is Newtonsoft.Json.Linq.JArray bridges
                && bridges.Count > 0)
            {
                BridgeLines = string.Join("\n", bridges.Select(b => b.ToString()));
                return MoatStatus.Ok;
            }

            return MoatStatus.Unreachable;
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
            return IsFetching ? MoatStatus.Unreachable : MoatStatus.Cancelled;
        }
    }
}
