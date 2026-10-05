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

internal static class AppPaths
{
    public static string BaseDir
        => MainWindow.Instance?.Cfg.BaseDir is { Length: > 0 } dir
            ? dir
            : AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string DataDir => Path.Combine(BaseDir, "Data");

    public static string DataFile(string name) => Path.Combine(DataDir, name);

    public static string XrayExe => Path.Combine(BaseDir, @"Data\Xray\xray.exe");

    public static string SingboxExe => Path.Combine(BaseDir, @"Data\sing_box\sing-box.exe");
}
