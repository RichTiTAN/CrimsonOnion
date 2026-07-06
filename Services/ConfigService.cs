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

using System.IO;
using Newtonsoft.Json;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services
{
    public static class ConfigService
    {
        public static void Save(AppConfig config, AppState state, string cfgFile, string lastConfig, string lastBridge, string lastCount)
        {
            var data = new
            {
                AutoStart = config.AutoStart,
                LaunchOnBoot = config.LaunchOnBoot,
                StartMinimized = config.StartMinimized,
                WindowLeft = config.WindowLeft,
                EnableAdBlock = config.EnableAdBlock,
                AllowLanConnections = config.AllowLanConnections,
                Language = config.Language,
                IsLogsOpen = state.IsLogsOpen,
                DebugMode = config.DebugMode,
                LastConfig = lastConfig,
                SelectedBridge = lastBridge,
                InstanceCount = lastCount,
                WindowTop = config.WindowTop,
                XrayMode = config.LastXrayMode,
                SplitTunnelMode = config.SplitTunnelMode,
                ManualSplit = config.LastManualSplit,
                AppSplit = config.LastAppSplit,
                BlockSplit = config.LastBlockSplit,
                EnableDirect = config.EnableDirect,
                CustomBridgeLine = config.CustomBridgeLine,
                EnableV2rayChain = config.EnableV2rayChain,
                V2rayChainJson = config.V2rayChainJson,
                EnableOutboundProxy = config.EnableOutboundProxy,
                OutboundProxyAddress = config.OutboundProxyAddress,
                OutboundProxyPort = config.OutboundProxyPort,
                OutboundProxyType = config.OutboundProxyType,
                OutboundProxyUser = config.OutboundProxyUser,
                OutboundProxyPass = config.OutboundProxyPass,
                EnableOutboundAuth = config.EnableOutboundAuth,
                EnableUpstreamDoh = config.EnableUpstreamDoh,
                UpstreamDohUrl = config.UpstreamDohUrl,
                CustomExitCountry = config.CustomExitCountry,
                MinimizeToTray = config.MinimizeToTray,
                ExpertHardwareAccel = config.ExpertHardwareAccel,
                ExpertStrictNodes = config.ExpertStrictNodes,
                ExpertFascistFirewall = config.ExpertFascistFirewall,
                ExpertCircuitBuildTimeout = config.ExpertCircuitBuildTimeout,
                ExpertKeepalivePeriod = config.ExpertKeepalivePeriod,
                ExpertNewCircuitPeriod = config.ExpertNewCircuitPeriod,
                ExpertMaxCircuitDirtiness = config.ExpertMaxCircuitDirtiness,
                ExpertNumEntryGuards = config.ExpertNumEntryGuards,
                ExpertEntryNodes = config.ExpertEntryNodes,
                ExpertExitNodes = config.ExpertExitNodes,
                ExpertExcludeNodes = config.ExpertExcludeNodes,
                ExpertExcludeExitNodes = config.ExpertExcludeExitNodes,
                ExpertCustomTorrc = config.ExpertCustomTorrc,
            };

            try
            {
                var dir = Path.GetDirectoryName(cfgFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(cfgFile, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        public static void Load(AppConfig config, AppState state, string cfgFile)
        {
            if (!File.Exists(cfgFile)) return;
            state.IsFirstLaunch = false;

            try
            {
                var json = File.ReadAllText(cfgFile);
                dynamic? s = JsonConvert.DeserializeObject(json);
                if (s == null) return;

                TrySet(s, "AutoStart", (Action<dynamic>)(v => config.AutoStart = (bool)v));
                TrySet(s, "LaunchOnBoot", (Action<dynamic>)(v => config.LaunchOnBoot = (bool)v));
                TrySet(s, "StartMinimized", (Action<dynamic>)(v => config.StartMinimized = (bool)v));
                TrySet(s, "WindowLeft", (Action<dynamic>)(v => config.WindowLeft = (double)v));
                TrySet(s, "EnableAdBlock", (Action<dynamic>)(v => config.EnableAdBlock = (bool)v));
                TrySet(s, "AllowLanConnections", (Action<dynamic>)(v => config.AllowLanConnections = (bool)v));
                TrySet(s, "Language", (Action<dynamic>)(v => config.Language = (string)v));
                TrySet(s, "IsLogsOpen", (Action<dynamic>)(v => state.IsLogsOpen = (bool)v));
                TrySet(s, "DebugMode", (Action<dynamic>)(v => config.DebugMode = (bool)v));
                TrySet(s, "LastConfig", (Action<dynamic>)(v => config.LastConfig = (string)v));
                TrySet(s, "SelectedBridge", (Action<dynamic>)(v => config.LastBridge = (string)v));
                TrySet(s, "InstanceCount", (Action<dynamic>)(v => config.LastCount = ((object)v).ToString()!));
                TrySet(s, "WindowTop", (Action<dynamic>)(v => config.WindowTop = (double)v));
                TrySet(s, "XrayMode", (Action<dynamic>)(v => config.LastXrayMode = (string)v));
                TrySet(s, "SplitTunnelMode", (Action<dynamic>)(v => config.SplitTunnelMode = (string)v));
                TrySet(s, "ManualSplit", (Action<dynamic>)(v => config.LastManualSplit = (string)v));
                TrySet(s, "AppSplit", (Action<dynamic>)(v => config.LastAppSplit = (string)v));
                TrySet(s, "BlockSplit", (Action<dynamic>)(v => config.LastBlockSplit = (string)v));
                TrySet(s, "EnableDirect", (Action<dynamic>)(v => config.EnableDirect = (bool)v));
                TrySet(s, "CustomBridgeLine", (Action<dynamic>)(v => config.CustomBridgeLine = (string)v));
                TrySet(s, "EnableV2rayChain", (Action<dynamic>)(v => config.EnableV2rayChain = (bool)v));
                TrySet(s, "V2rayChainJson", (Action<dynamic>)(v => config.V2rayChainJson = (string)v));
                TrySet(s, "EnableOutboundProxy", (Action<dynamic>)(v => config.EnableOutboundProxy = (bool)v));
                TrySet(s, "OutboundProxyAddress", (Action<dynamic>)(v => config.OutboundProxyAddress = (string)v));
                TrySet(s, "OutboundProxyPort", (Action<dynamic>)(v => config.OutboundProxyPort = (string)v));
                TrySet(s, "OutboundProxyType", (Action<dynamic>)(v => config.OutboundProxyType = (string)v));
                TrySet(s, "OutboundProxyUser", (Action<dynamic>)(v => config.OutboundProxyUser = (string)v));
                TrySet(s, "OutboundProxyPass", (Action<dynamic>)(v => config.OutboundProxyPass = (string)v));
                TrySet(s, "EnableOutboundAuth", (Action<dynamic>)(v => config.EnableOutboundAuth = (bool)v));
                TrySet(s, "EnableUpstreamDoh", (Action<dynamic>)(v => config.EnableUpstreamDoh = (bool)v));
                TrySet(s, "UpstreamDohUrl", (Action<dynamic>)(v => config.UpstreamDohUrl = (string)v));
                TrySet(s, "CustomExitCountry", (Action<dynamic>)(v => config.CustomExitCountry = (string)v));
                TrySet(s, "MinimizeToTray", (Action<dynamic>)(v => config.MinimizeToTray = (bool)v));
                TrySet(s, "ExpertHardwareAccel", (Action<dynamic>)(v => config.ExpertHardwareAccel = (bool)v));
                TrySet(s, "ExpertStrictNodes", (Action<dynamic>)(v => config.ExpertStrictNodes = (bool)v));
                TrySet(s, "ExpertFascistFirewall", (Action<dynamic>)(v => config.ExpertFascistFirewall = (bool)v));
                TrySet(s, "ExpertCircuitBuildTimeout", (Action<dynamic>)(v => config.ExpertCircuitBuildTimeout = (string)v));
                TrySet(s, "ExpertKeepalivePeriod", (Action<dynamic>)(v => config.ExpertKeepalivePeriod = (string)v));
                TrySet(s, "ExpertNewCircuitPeriod", (Action<dynamic>)(v => config.ExpertNewCircuitPeriod = (string)v));
                TrySet(s, "ExpertMaxCircuitDirtiness", (Action<dynamic>)(v => config.ExpertMaxCircuitDirtiness = (string)v));
                TrySet(s, "ExpertNumEntryGuards", (Action<dynamic>)(v => config.ExpertNumEntryGuards = (string)v));
                TrySet(s, "ExpertEntryNodes", (Action<dynamic>)(v => config.ExpertEntryNodes = (string)v));
                TrySet(s, "ExpertExitNodes", (Action<dynamic>)(v => config.ExpertExitNodes = (string)v));
                TrySet(s, "ExpertExcludeNodes", (Action<dynamic>)(v => config.ExpertExcludeNodes = (string)v));
                TrySet(s, "ExpertExcludeExitNodes", (Action<dynamic>)(v => config.ExpertExcludeExitNodes = (string)v));
                TrySet(s, "ExpertCustomTorrc", (Action<dynamic>)(v => config.ExpertCustomTorrc = (string)v));

                if (config.LastConfig == "Stable" || config.LastConfig == "Fast")
                    config.LastConfig = "Optimized";
                if (config.LastBridge == "snowflake" && config.LastXrayMode == "VPN Mode")
                    config.LastXrayMode = "Proxy Mode";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config load error: {ex.Message}");
            }
        }

        private static void TrySet(dynamic s, string key, Action<dynamic> setter)
        {
            try
            {
                var val = s[key];
                if (val != null) setter(val);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set config key '{key}': {ex.Message}");
            }
        }
    }
}
