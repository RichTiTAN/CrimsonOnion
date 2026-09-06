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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CrimsonOnion.Services;

public static class VpnEngineService
{
    // ── Allowed process names for validated kills ───────────────────────────
    private static readonly string[] _managedProcessNames =
        { "xray", "sing-box", "sing_box", "tor", "lyrebird" };

    // ── Log deduplication ───────────────────────────────────────────────────
    private static readonly ConcurrentDictionary<string, byte> _seenLogs = new();

    // ── Public API ──────────────────────────────────────────────────────────

    public static void KillPid(int? pid)
    {
        if (!pid.HasValue) return;
        try
        {
            using var p = Process.GetProcessById(pid.Value);
            if (IsManagedProcess(p.ProcessName) && !p.HasExited)
            {
                p.Kill();
                p.WaitForExit(1000);
            }
        }
        catch (ArgumentException) { }
        catch (Exception ex) { SimpleLogger.Log(ex); }
    }

    public static void KillPidRef(ref int? pidRef)
    {
        KillPid(pidRef);
        pidRef = null;
    }

    public static void KillManagedProcesses(string baseDir, params string[] names)
    {
        DnsttManager.StopAllTunnels();
        if (names == null || names.Length == 0) return;

        var paths = new[]
        {
            Path.Combine(baseDir, @"Data\Xray\xray.exe"),
            Path.Combine(baseDir, @"Data\sing_box\sing-box.exe"),
            Path.Combine(baseDir, @"Data\TorBin\tor.exe"),
            Path.Combine(baseDir, @"Data\TorBin\lyrebird.exe")
        };

        try
        {
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    if (!names.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        var exePath = p.MainModule?.FileName ?? string.Empty;
                        if (paths.Any(path => string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase))
                            || exePath.IndexOf(@"Data\Tors\", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            p.Kill();
                            p.WaitForExit(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is not InvalidOperationException and not System.ComponentModel.Win32Exception)
                            SimpleLogger.Log(ex);
                    }
                }
            }
        }
        catch (Exception ex) { SimpleLogger.Log(ex); }
    }

    public static int? StartDebugProcess(
        string exePath, string args, string workingDir, string label, bool warnOnly = true)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = exePath,
                Arguments              = args,
                WorkingDirectory       = workingDir,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            var proc = new Process { StartInfo = psi };
            proc.Start();
            try { JobManager.AddProcess(proc); } catch { }

            int pid = proc.Id;

            _ = Task.Run(async () =>
            {
                try
                {
                    var outTask = Task.Run(async () =>
                    {
                        try
                        {
                            string? line;
                            while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
                                if (!string.IsNullOrWhiteSpace(line) && ShouldLog(line, warnOnly))
                                    SimpleLogger.Log($"[{label}] {line}");
                        }
                        catch { }
                    });
                    var errTask = Task.Run(async () =>
                    {
                        try
                        {
                            string? line;
                            while ((line = await proc.StandardError.ReadLineAsync()) != null)
                                if (!string.IsNullOrWhiteSpace(line) && ShouldLog(line, warnOnly))
                                    SimpleLogger.Log($"[{label}] {line}");
                        }
                        catch { }
                    });
                    await Task.WhenAll(outTask, errTask);
                }
                catch { }
                finally { try { proc.Dispose(); } catch { } }
            });

            return pid;
        }
        catch (Exception ex)
        {
            SimpleLogger.Log(ex);
            return null;
        }
    }

    public static void ClearSeenLogs() => _seenLogs.Clear();

    // ── Internals ───────────────────────────────────────────────────────────

    private static bool IsManagedProcess(string name)
        => _managedProcessNames.Any(n => name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);

    internal static bool ShouldLog(string line, bool warnOnly)
    {
        if (!warnOnly) return true;

        if (line.IndexOf("is relative and will resolve to", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        bool isWarn = line.IndexOf("warn",  StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("alert", StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("emerg", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isWarn) return false;

        string payload = line;
        string[] tags  = { "[warn]", "[warning]", "[error]", "[err]", "[fatal]", "[alert]", "[emerg]" };
        foreach (var tag in tags)
        {
            int idx = line.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                payload = line.Substring(idx + tag.Length).Trim();
                break;
            }
        }

        if (_seenLogs.Count > 2000) _seenLogs.Clear();
        return payload.Length > 0 && _seenLogs.TryAdd(payload, 1);
    }
}
