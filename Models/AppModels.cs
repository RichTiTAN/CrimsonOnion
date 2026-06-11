using Newtonsoft.Json;

namespace CrimsonOnion.Models
{
    public class AppConfig
    {
        public string CurrentVersion { get; set; } = "5.2.3";
        public string MinAutoUpdateVersion { get; set; } = "5.2.1";

        // Resolved at runtime, not persisted
        [JsonIgnore] public string BaseDir { get; set; } = "";
        [JsonIgnore] public string ScriptPath { get; set; } = "";
        [JsonIgnore] public string CfgFile { get; set; } = "";
        [JsonIgnore] public string XrayDir { get; set; } = "";
        [JsonIgnore] public string HaPath { get; set; } = "";
        [JsonIgnore] public string SbDir { get; set; } = "";

        // Persisted user preferences
        public bool AutoStart { get; set; } = true;
        public bool LaunchOnBoot { get; set; } = false;
        public bool DebugMode { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public string LastConfig { get; set; } = "Optimized";
        public string LastBridge { get; set; } = "meek_lite";
        public string LastCount { get; set; } = "6";
        public string LastXrayMode { get; set; } = "Proxy Mode";
        public string LastManualSplit { get; set; } = "";
        public string LastAppSplit { get; set; } = "";
        public string LastBlockSplit { get; set; } = "";
        public bool EnableDirect { get; set; } = false;
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
        public bool EnableUpstreamDoh { get; set; } = false;
        public string UpstreamDohUrl { get; set; } = "https://cloudflare-dns.com/dns-query";
        public string CustomExitCountry { get; set; } = "us";
        public bool MinimizeToTray { get; set; } = false;
        public bool EnableAdBlock { get; set; } = false;
        public bool AllowLanConnections { get; set; } = false;
        public bool IsLogsOpen { get; set; } = false;

        // Expert config
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
        public string PreviousBridge { get; set; } = "meek_lite";
        public string PreviousConfig { get; set; } = "Optimized";
        public string LanIp { get; set; } = "UNKNOWN";
        public DateTime? SessionStartTime { get; set; } = null;
        public long LastTotalBytes { get; set; } = 0;
        public long SessionDataBytes { get; set; } = 0;
        public double[] SpeedSamples { get; set; } = new double[5];
        public string TempProxyType { get; set; } = "SOCKS5";
    }

    public class BridgeEntry
    {
        public string Plugin { get; set; } = "";
        public string[] Lines { get; set; } = Array.Empty<string>();
    }
}
