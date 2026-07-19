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
        public const string AppVersion = "2.1.0";
        
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
                
                // FIX: Close the file stream so the zip file isn't locked by the app when we try to extract it!
                fs.Close();

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
