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

using Newtonsoft.Json;

namespace CrimsonOnion.Models
{
    public class AppConfig
    {

        [JsonIgnore] public string BaseDir { get; set; } = "";
        [JsonIgnore] public string CfgFile { get; set; } = "";
        [JsonIgnore] public string XrayDir { get; set; } = "";
        [JsonIgnore] public string HaPath { get; set; } = "";
        [JsonIgnore] public string SbDir { get; set; } = "";

        public bool AutoStart { get; set; } = true;
        public bool LaunchOnBoot { get; set; } = false;
        public bool DebugMode { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public string LastConfig { get; set; } = "Optimized";
        public string LastBridge { get; set; } = "Direct";
        public string LastCount { get; set; } = "6";
        public string LastXrayMode { get; set; } = "Proxy Mode";
        public string SplitTunnelMode { get; set; } = "DISABLED"; 
        public string LastManualSplit { get; set; } = "";
        public string LastAppSplit { get; set; } = "";
        public string LastBlockSplit { get; set; } = "";
        public bool EnableDirect { get; set; } = false;
        public bool EnableDirectUDP { get; set; } = false;
        public string CustomBridgeLine { get; set; } = "";
        public string V2rayChainJson { get; set; } = "";
        public bool EnableV2rayChain { get; set; } = false;
        public string OutboundProxyAddress { get; set; } = "";
        public string OutboundProxyPort { get; set; } = "";
        public string OutboundProxyType { get; set; } = "SOCKS5";
        public bool EnableOutboundProxy { get; set; } = false;
        public string OutboundProxyUser { get; set; } = "";
        public string OutboundProxyPass { get; set; } = "";
        public bool EnableOutboundAuth { get; set; } = false;

        public bool EnableAdapterBinding { get; set; } = false;
        public string SelectedAdapterName { get; set; } = "";
        public string SelectedAdapterIp { get; set; } = "";

        public bool EnableUpstreamDoh { get; set; } = false;
        public string UpstreamDohUrl { get; set; } = "https://cloudflare-dns.com/dns-query";
        public bool EnableSystemDns { get; set; } = false;
        public string SystemDnsPrimary { get; set; } = "";
        public string SystemDnsSecondary { get; set; } = "";
        public string CustomExitCountry { get; set; } = "us";
        public bool MinimizeToTray { get; set; } = false;
        public bool EnableAdBlock { get; set; } = false;
        public bool AllowLanConnections { get; set; } = false;
        public bool EnableLanAuth       { get; set; } = false;
        public string LanAuthUsername   { get; set; } = "";
        public string LanAuthPassword   { get; set; } = "";
        public string Language { get; set; } = "ENGLISH";
        public string ThemeColor { get; set; } = "Crimson";

        public bool ExpertHardwareAccel { get; set; } = false;
        public bool ExpertStrictNodes { get; set; } = false;
        public bool ExpertFascistFirewall { get; set; } = false;
        public string ExpertCircuitBuildTimeout { get; set; } = "";
        public string ExpertKeepalivePeriod { get; set; } = "";
        public string ExpertNewCircuitPeriod { get; set; } = "";
        public string ExpertMaxCircuitDirtiness { get; set; } = "";
        public string ExpertNumEntryGuards { get; set; } = "";
        public string ExpertEntryNodes { get; set; } = "";
        public string ExpertExitNodes { get; set; } = "";
        public string ExpertExcludeNodes { get; set; } = "";
        public string ExpertExcludeExitNodes { get; set; } = "";
        public string ExpertCustomTorrc { get; set; } = "";
    }

    public class AppState
    {
        public bool IsFirstLaunch { get; set; } = true;
        public bool IsConnected { get; set; } = false;
        public bool IsEngineRunning { get; set; } = false;
        public bool AbortBoot { get; set; } = false;
        public bool IsGeoTracing { get; set; } = false;
        public bool IsAdvancedOpen { get; set; } = false;
        public bool IsLogsOpen { get; set; } = false;
        public bool IgnoreComboChange { get; set; } = false;
        public bool AppInitialized { get; set; } = false;
        public string PreviousBridge { get; set; } = "Direct";
        public string PreviousConfig { get; set; } = "Optimized";
        public string LanIp { get; set; } = "UNKNOWN";
        public DateTime? SessionStartTime { get; set; } = null;
        public long LastTotalBytes { get; set; } = 0;
        public long SessionDataBytes { get; set; } = 0;
        public double[] SpeedSamples { get; set; } = new double[5];
        public string TempProxyType { get; set; } = "SOCKS5";
        public int[] TorPcts { get; set; } = System.Linq.Enumerable.Repeat(-1, 32).ToArray();
    }

    public class BridgeEntry
    {
        public string Plugin { get; set; } = "";
        public string[] Lines { get; set; } = Array.Empty<string>();
    }
}
