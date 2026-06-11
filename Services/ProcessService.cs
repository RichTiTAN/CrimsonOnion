using System.Diagnostics;
using System.IO;

namespace CrimsonOnion.Services
{
    public static class ProcessService
    {
        public static Process? StartProcessDirect(string filePath, string arguments = "", string workingDirectory = "", bool hidden = true)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = arguments,
                    WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? "" : workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = hidden,
                    WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
                };
                var process = Process.Start(psi);
                if (process != null)
                {
                    try { if (!process.HasExited) process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                }
                return process;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Process launch failed for '{filePath}': {ex.Message}");
                return null;
            }
        }

        public static void KillProcess(int? pid)
        {
            if (pid == null) return;
            try
            {
                using var p = Process.GetProcessById(pid.Value);
                p.Kill();
            }
            catch { }
        }

        public static void KillAppProcesses(string[] names, string[] appPaths)
        {
            foreach (var name in names)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var p in procs)
                    {
                        using (p)
                        {
                            try
                            {
                                if (appPaths.Any(path => string.Equals(path, p.MainModule?.FileName, StringComparison.OrdinalIgnoreCase)))
                                    p.Kill();
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        public static void UpdateBootScheduledTask(bool enable, string exePath, string workingDir)
        {
            const string taskName = "CrimsonOnion_AutoStart";
            try
            {
                if (enable)
                {
                    // Create a scheduled task at logon
                    var args = $"/Create /F /RL HIGHEST /SC ONLOGON /TN \"{taskName}\" /TR \"\\\"{exePath}\\\"\" /DELAY 0000:05";
                    var psi = new ProcessStartInfo("schtasks.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(3000);
                }
                else
                {
                    var args = $"/Delete /F /TN \"{taskName}\"";
                    var psi = new ProcessStartInfo("schtasks.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(3000);
                }

                var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var oldLnk = Path.Combine(startupFolder, "CrimsonOnion.lnk");
                if (File.Exists(oldLnk)) File.Delete(oldLnk);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Boot task error: {ex.Message}");
                throw;
            }
        }

        public static int ReadBootstrapPct(string torLogPath)
        {
            if (!File.Exists(torLogPath)) return -1;
            try
            {
                using var fs = new FileStream(torLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length > 8192) fs.Seek(-8192, SeekOrigin.End);
                using var sr = new StreamReader(fs);
                var content = sr.ReadToEnd();
                
                int idx = content.LastIndexOf("Bootstrapped ");
                if (idx >= 0)
                {
                    idx += 13; // length of "Bootstrapped "
                    int endIdx = content.IndexOf('%', idx);
                    if (endIdx > idx)
                    {
                        if (int.TryParse(content.Substring(idx, endIdx - idx), out int pct))
                            return pct;
                    }
                }
            }
            catch { }
            return -1;
        }
    }
}
