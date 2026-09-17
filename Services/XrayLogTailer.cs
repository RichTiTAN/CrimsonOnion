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
internal sealed class XrayLogTailer
{
    public const int MaxLines = 15;
    private readonly System.Collections.Generic.List<string> _lines = new();
    private long _position;
    private int _reading;
    public System.Collections.Generic.IReadOnlyList<string> Lines => _lines;
    public long Position => System.Threading.Interlocked.Read(ref _position);
    public bool TryBeginRead() => System.Threading.Interlocked.CompareExchange(ref _reading, 1, 0) == 0;
    public void EndRead() => System.Threading.Interlocked.Exchange(ref _reading, 0);
    public void Append(System.Collections.Generic.IEnumerable<string> lines)
    {
        foreach (var line in lines) _lines.Add(line);
        if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
    }
    public void Reset()
    {
        System.Threading.Interlocked.Exchange(ref _position, 0);
        _lines.Clear();
    }
    public string[] ReadNewLines(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return System.Array.Empty<string>();

            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            var currentPos = System.Threading.Interlocked.Read(ref _position);
            if (fs.Length < currentPos) { System.Threading.Interlocked.Exchange(ref _position, 0); currentPos = 0; }
            if (fs.Length <= currentPos) return System.Array.Empty<string>();

            fs.Seek(currentPos, System.IO.SeekOrigin.Begin);

            var found = new System.Collections.Generic.Queue<string>(MaxLines);
            using (var sr = new System.IO.StreamReader(fs, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true))
            {
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    var shaped = Shape(line);
                    if (shaped == null) continue;

                    found.Enqueue(shaped);
                    if (found.Count > MaxLines) found.Dequeue();
                }
            }

            System.Threading.Interlocked.Exchange(ref _position, fs.Length);
            return found.ToArray();
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
            return System.Array.Empty<string>();
        }
    }
    public static string? Shape(string line)
    {
        if (line.IndexOf("accepted", System.StringComparison.Ordinal) < 0
            && line.IndexOf("proxy", System.StringComparison.Ordinal) < 0) return null;
        if (line.IndexOf(":10899", System.StringComparison.Ordinal) >= 0) return null;

        int firstSpace = line.IndexOf(' ');
        if (firstSpace <= 0) return null;

        int secondSpace = line.IndexOf(' ', firstSpace + 1);
        if (secondSpace <= 0) return null;

        var rest = line.AsSpan(secondSpace + 1);
        if (!rest.StartsWith("127.0.0.1:")) return rest.ToString();

        int hopSpace = rest.IndexOf(' ');
        return hopSpace > 0 ? rest.Slice(hopSpace + 1).ToString() : rest.ToString();
    }
}
