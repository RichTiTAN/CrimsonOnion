using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;

namespace CrimsonOnion.Services
{
    public static class UpdateService
    {
        public const string AppVersion = "2.0.1";
        
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static async Task<(string? remoteVer, string? remoteMin)> CheckForUpdatesAsync(CancellationToken token = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("https://raw.githubusercontent.com/RichTiTAN/CrimsonOnion/main/version.json", token);
                response.EnsureSuccessStatusCode();
                
                var raw = await response.Content.ReadAsStringAsync(token);
                var json = JObject.Parse(raw);
                var remoteVer = json["version"]?.ToString() ?? "0.0.0";
                var remoteMin = json["minAutoUpdateVersion"]?.ToString() ?? "0.0.0";

                if (Version.Parse(remoteVer) > Version.Parse(AppVersion))
                {
                    return (remoteVer, remoteMin);
                }
            }
            catch
            {
            }
            return (null, null);
        }

        public static async Task DownloadAndInstallUpdateAsync(string remoteVersion, string baseDir, Action<string> progressCallback, CancellationToken token)
        {
            var zipUrl = "https://github.com/RichTiTAN/CrimsonOnion/releases/latest/download/CrimsonOnion.zip";
            var zipPath = Path.Combine(baseDir, "update_temp.zip");
            var extPath = Path.Combine(baseDir, "update_extracted");
            
            if (Directory.Exists(extPath)) Directory.Delete(extPath, true);

            progressCallback($"DOWNLOADING UPDATE... 0% (CLICK TO CANCEL)");

            try
            {
                using var dlResponse = await _httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, token);
                dlResponse.EnsureSuccessStatusCode();
                
                var total = dlResponse.Content.Headers.ContentLength ?? -1L;
                
                using var fs = File.Create(zipPath);
                using var stream = await dlResponse.Content.ReadAsStreamAsync(token);
                var buffer = new byte[81920];
                long downloaded = 0;
                int read;
                int lastPct = -1;
                
                while ((read = await stream.ReadAsync(buffer, token)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read), token);
                    downloaded += read;
                    if (total > 0)
                    {
                        int pct = (int)(downloaded * 100 / total);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            Dispatcher.UIThread.Post(() => progressCallback($"DOWNLOADING UPDATE... {pct}% (CLICK TO CANCEL)"));
                        }
                    }
                }

                Dispatcher.UIThread.Post(() => progressCallback("EXTRACTING UPDATE..."));
                
                await Task.Run(() => {
                    token.ThrowIfCancellationRequested();
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extPath, true);
                }, token);

                var exeFile = Directory.GetFiles(extPath, "CrimsonOnion.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (exeFile == null) throw new Exception("CrimsonOnion.exe not found in the downloaded ZIP!");

                var sourceDir = Path.GetDirectoryName(exeFile)!;
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                
                var cmdArgs = $"/c ping 127.0.0.1 -n 4 > nul & xcopy /Y /E /H /C /I \"{sourceDir}\\*\" \"{baseDir}\" & rmdir /S /Q \"{extPath}\" & del /Q \"{zipPath}\" & start \"\" \"{currentExe}\"";

                using (Process.Start(new ProcessStartInfo("cmd.exe", cmdArgs) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true }))
                {
                }
            }
            catch
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (Directory.Exists(extPath)) Directory.Delete(extPath, true); } catch { }
                throw;
            }
        }
    }
}
