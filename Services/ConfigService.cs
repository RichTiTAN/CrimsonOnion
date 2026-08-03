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
                EnableLanAuth = config.EnableLanAuth,
                LanAuthUsername = config.LanAuthUsername,
                LanAuthPassword = config.LanAuthPassword,
                Language = config.Language,
                IsLogsOpen = state.IsLogsOpen,
                DebugMode = config.DebugMode,
                ThemeColor = config.ThemeColor,
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
                EnableDirectUDP = config.EnableDirectUDP,
                CustomBridgeLine = config.CustomBridgeLine,
                EnableV2rayChain = config.EnableV2rayChain,
                V2rayChainJson = config.V2rayChainJson,
                EnableOutboundProxy = config.EnableOutboundProxy,
                OutboundProxyType = config.OutboundProxyType,
                OutboundProxyAddress = config.OutboundProxyAddress,
                OutboundProxyPort = config.OutboundProxyPort,
                EnableOutboundAuth = config.EnableOutboundAuth,
                OutboundProxyUser = config.OutboundProxyUser,
                OutboundProxyPass = config.OutboundProxyPass,
                EnableAdapterBinding = config.EnableAdapterBinding,
                SelectedAdapterName = config.SelectedAdapterName,
                SelectedAdapterIp = config.SelectedAdapterIp,
                EnableUpstreamDoh = config.EnableUpstreamDoh,
                UpstreamDohUrl = config.UpstreamDohUrl,
                EnableSystemDns = config.EnableSystemDns,
                SystemDnsPrimary = config.SystemDnsPrimary,
                SystemDnsSecondary = config.SystemDnsSecondary,
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
                SimpleLogger.Log(ex);
            }
        }

                public static void Load(AppConfig config, AppState state, string cfgFile)
        {
            if (!File.Exists(cfgFile)) return;
            state.IsFirstLaunch = false;

            try
            {
                var json = File.ReadAllText(cfgFile);
                JsonConvert.PopulateObject(json, config);

                var jobj = Newtonsoft.Json.Linq.JObject.Parse(json);
                if (jobj["IsLogsOpen"] != null)
                    state.IsLogsOpen = jobj.Value<bool>("IsLogsOpen");

                if (config.LastConfig == "Stable" || config.LastConfig == "Fast")
                    config.LastConfig = "Optimized";
                if (config.LastBridge == "snowflake" && config.LastXrayMode == "VPN Mode")
                    config.LastXrayMode = "Proxy Mode";
            }
            catch (Exception ex)
            {
                SimpleLogger.Log(ex);
            }
        }
    }
}
