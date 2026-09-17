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

using Newtonsoft.Json.Linq;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services;
internal sealed class XraySupervisor
{
    internal const string VpnMode = XrayModes.VpnMode;
    internal const string ProxyMode = XrayModes.ProxyMode;
    internal const string ClearProxy = XrayModes.ClearProxy;
    private const string XrayExeRelativePath    = @"Data\Xray\xray.exe";
    private const string SingboxExeRelativePath = @"Data\sing_box\sing-box.exe";
    private const string CoreConfigFileName = "config.json";
    private const string CoreConfigArgs     = "run -c " + CoreConfigFileName;
    private const string AdapterConfigFileName = "adapter_config.json";
    private const string AdapterConfigArgs     = "run -c " + AdapterConfigFileName;
    internal const string AccessLogFileName        = "access.log";
    internal const string ErrorLogFileName         = "error.log";
    internal const string RotatedAccessLogFileName = "access.log.tmp";

    private readonly VpnRuntimeState _vpn;
    private readonly AppConfig _cfg;
    private readonly Func<string, string, string, int?> _startDirect;
    private readonly Func<string, string, string, string, int?> _startDebug;
    private readonly Action<int?> _kill;
    internal XraySupervisor(
        VpnRuntimeState vpn,
        AppConfig cfg,
        Func<string, string, string, int?> startDirect,
        Func<string, string, string, string, int?> startDebug,
        Action<int?>? killProcess = null)
    {
        _vpn         = vpn;
        _cfg         = cfg;
        _startDirect = startDirect;
        _startDebug  = startDebug;
        _kill        = killProcess ?? VpnEngineService.KillPid;
    }

    // -- Starting ------------------------------------------------------------
    internal bool StartCore(string mode)
    {
        if (!XrayConfigWriter.Write(_cfg, _cfg.XrayDir)) return false;
        if (mode == VpnMode && !SingboxConfigWriter.Write(_cfg, _cfg.SbDir)) return false;

        int? xrayPid = Spawn(CoreExePath, CoreConfigArgs, _cfg.XrayDir, "Xray");
        int? sbPid   = mode == VpnMode
            ? Spawn(SingboxExePath, CoreConfigArgs, _cfg.SbDir, "SingBox")
            : null;

        if (_cfg.DebugMode)
        {
            _vpn.XrayDebugPid = xrayPid;
            if (mode == VpnMode) _vpn.SbDebugPid = sbPid;
        }
        else
        {
            _vpn.XrayPid = xrayPid;
            if (mode == VpnMode) _vpn.SbPid = sbPid;
        }

        return true;
    }
    internal bool StartAdapterBinding()
    {
        string? configJson = BuildAdapterConfig(_cfg);
        if (configJson == null) return false;

        if (!Directory.Exists(_cfg.XrayDir)) Directory.CreateDirectory(_cfg.XrayDir);
        File.WriteAllText(Path.Combine(_cfg.XrayDir, AdapterConfigFileName), configJson);

        int? pid = Spawn(CoreExePath, AdapterConfigArgs, _cfg.XrayDir, "AdapterXray");

        if (_cfg.DebugMode)
        {
            _vpn.AdapterXrayDebugPid = pid;
        }
        else
        {
            _vpn.AdapterXrayPid = pid;
        }

        return true;
    }

    // -- Stopping ------------------------------------------------------------
    internal void StopAll()
    {
        Kill(ref _vpn.XrayDebugPid);
        Kill(ref _vpn.SbDebugPid);
        Kill(ref _vpn.AdapterXrayDebugPid);
        Kill(ref _vpn.XrayPid);
        Kill(ref _vpn.SbPid);
        Kill(ref _vpn.AdapterXrayPid);
    }
    internal void StopCore()
    {
        Kill(ref _vpn.XrayDebugPid);
        Kill(ref _vpn.XrayPid);
    }

    // -- Logs ----------------------------------------------------------------
    internal string AccessLogPath => Path.Combine(_cfg.XrayDir, AccessLogFileName);
    internal string ErrorLogPath => Path.Combine(_cfg.XrayDir, ErrorLogFileName);
    internal void DeleteLogs(bool rotated = false)
    {
        TryDeleteFile(AccessLogPath);
        TryDeleteFile(ErrorLogPath);
        if (rotated) TryDeleteFile(Path.Combine(_cfg.XrayDir, RotatedAccessLogFileName));
    }
    internal void TruncateLogs()
    {
        foreach (string path in new[] { AccessLogPath, ErrorLogPath })
        {
            try
            {
                if (File.Exists(path))
                {
                    using var log = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite);
                }
            }
            catch (Exception ex) { SimpleLogger.Log(ex); }
        }
    }

    // -- Planning ------------------------------------------------------------
    internal static string? BuildAdapterConfig(AppConfig cfg)
    {
        bool useAdapter  = cfg.EnableAdapterBinding && !string.IsNullOrWhiteSpace(cfg.SelectedAdapterIp);
        bool useOutbound = cfg.EnableOutboundProxy
            && !string.IsNullOrEmpty(cfg.OutboundProxyAddress)
            && !string.IsNullOrEmpty(cfg.OutboundProxyPort);

        if (!useAdapter && !useOutbound) return null;

        var configObj = new JObject
        {
            ["log"] = new JObject { ["loglevel"] = "warning" },
            ["inbounds"] = new JArray
            {
                new JObject
                {
                    ["port"] = 10819,
                    ["listen"] = "127.0.0.1",
                    ["protocol"] = "socks",
                    ["settings"] = new JObject { ["auth"] = "noauth", ["udp"] = true }
                }
            }
        };

        var outbounds = new JArray();

        var boundOutbound = new JObject
        {
            ["tag"] = "bound_out"
        };

        if (useAdapter)
        {
            boundOutbound["protocol"] = "freedom";
            boundOutbound["settings"] = new JObject();
            boundOutbound["sendThrough"] = cfg.SelectedAdapterIp;
        }
        else if (useOutbound)
        {
            boundOutbound["protocol"] = cfg.OutboundProxyType == "HTTPS" ? "http" : "socks";

            var serverObj = new JObject
            {
                ["address"] = cfg.OutboundProxyAddress,
                ["port"] = int.TryParse(cfg.OutboundProxyPort, out int port) ? port : 1080
            };

            if (cfg.EnableOutboundAuth
                && !string.IsNullOrEmpty(cfg.OutboundProxyUser)
                && !string.IsNullOrEmpty(cfg.OutboundProxyPass))
            {
                var userObj = new JObject
                {
                    ["user"] = cfg.OutboundProxyUser,
                    ["pass"] = cfg.OutboundProxyPass
                };
                serverObj["users"] = new JArray { userObj };
            }

            boundOutbound["settings"] = new JObject
            {
                ["servers"] = new JArray { serverObj }
            };
        }

        outbounds.Add(boundOutbound);

        var directOutbound = new JObject
        {
            ["protocol"] = "freedom",
            ["tag"] = "direct_out",
            ["settings"] = new JObject()
        };
        outbounds.Add(directOutbound);

        var localRule = new JObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct_out"
        };
        localRule["ip"] = new JArray { "127.0.0.0/8", "::1/128", "geoip:private" };

        var allRule = new JObject
        {
            ["type"] = "field",
            ["network"] = "tcp,udp",
            ["outboundTag"] = "bound_out"
        };

        configObj["outbounds"] = outbounds;
        configObj["routing"] = new JObject
        {
            ["domainStrategy"] = "AsIs",
            ["rules"] = new JArray { localRule, allRule }
        };

        return configObj.ToString();
    }

    // -- Internals -----------------------------------------------------------
    private int? Spawn(string exePath, string args, string workingDir, string label)
        => _cfg.DebugMode
            ? _startDebug(exePath, args, workingDir, label)
            : _startDirect(exePath, args, workingDir);
    private void Kill(ref int? pidRef)
    {
        int? pid = pidRef;
        pidRef = null;
        _kill(pid);
    }
    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { SimpleLogger.Log(ex); }
    }

    private string CoreExePath    => Path.Combine(_cfg.BaseDir, XrayExeRelativePath);
    private string SingboxExePath => Path.Combine(_cfg.BaseDir, SingboxExeRelativePath);
}
