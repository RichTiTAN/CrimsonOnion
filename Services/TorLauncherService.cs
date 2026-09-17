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

namespace CrimsonOnion.Services;
internal sealed class TorLauncherService
{
    public const int ControlPortBase = 20050;
    public const int MaxTors = 8;

    private readonly List<TorControlClient> _clients = new();
    public event Action<int, int>? ProgressUpdated;
    public event Action<int>? ConnectionDropped;
    public void Launch(int torIndex, string torPath)
    {
        var existing = _clients.FirstOrDefault(c => c.TorIndex == torIndex);
        if (existing != null)
        {
            existing.Dispose();
            _clients.Remove(existing);
        }

        var client = new TorControlClient(ControlPortBase + torIndex, CookiePath(torPath), torIndex);

        client.BootstrapProgressUpdated += (idx, pct) =>
        {
            if (!_clients.Contains(client)) return;
            ProgressUpdated?.Invoke(idx, pct);
        };

        client.ConnectionDropped += idx =>
        {
            if (!_clients.Contains(client)) return;
            ConnectionDropped?.Invoke(idx);
        };

        _clients.Add(client);
        client.Start();
    }
    public void StopAll()
    {
        foreach (var client in _clients) client.Dispose();
        _clients.Clear();
    }
    public static void KillProcesses(string baseDir)
        => VpnEngineService.KillManagedProcesses(baseDir, "tor", "lyrebird");
    public static void DeleteStaleArtifacts(string torPath)
    {
        TryDeleteFile(Path.Combine(torPath, "tor.log"));
        TryDeleteFile(CookiePath(torPath));
    }
    public static string CookiePath(string torPath) => Path.Combine(torPath, "Data", "control_auth_cookie");
    public static bool IsValidIndex(int torIndex) => torIndex >= 1 && torIndex <= MaxTors;

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { SimpleLogger.Log(ex); }
    }
}
