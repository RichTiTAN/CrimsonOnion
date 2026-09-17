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
internal static class CancellationTokens
{
    internal static bool CancelAndDispose(ref System.Threading.CancellationTokenSource? cts)
    {
        var source = cts;
        if (source == null) return false;

        cts = null;

        try
        {
            source.Cancel();
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
        }

        try
        {
            source.Dispose();
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
        }

        return true;
    }
    internal static void Forget(ref System.Threading.CancellationTokenSource? cts)
    {
        var source = cts;
        if (source == null) return;

        cts = null;

        try
        {
            source.Dispose();
        }
        catch (System.Exception ex)
        {
            SimpleLogger.Log(ex);
        }
    }
}
