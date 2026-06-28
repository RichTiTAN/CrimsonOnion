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
                var process = new Process { StartInfo = psi };
                process.Start();
                try { if (!process.HasExited) process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
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
                    if (p != null && p.ExitCode != 0) {
                        string err = p.StandardError.ReadToEnd();
                        throw new Exception("schtasks exit code " + p.ExitCode + " " + err);
                    }
                }
                else
                {
                    var args = $"/Delete /F /TN \"{taskName}\"";
                    var psi = new ProcessStartInfo("schtasks.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(3000);
                    if (p != null && p.ExitCode != 0) {
                        string err = p.StandardError.ReadToEnd();
                        throw new Exception("schtasks exit code " + p.ExitCode + " " + err);
                    }
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
    }
}

