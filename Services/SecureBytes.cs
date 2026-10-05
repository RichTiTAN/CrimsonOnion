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

using System.Runtime.InteropServices;

namespace CrimsonOnion.Services
{
    internal static class SecureBytes
    {
        private const int CryptprotectUiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int CbData;
            public IntPtr PbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob pDataIn, string? szDataDescr, IntPtr pOptionalEntropy,
            IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob pDataIn, IntPtr ppszDataDescr,
            IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        public static byte[] Protect(byte[] plain)
        {
            if (plain == null || plain.Length == 0) return Array.Empty<byte>();
            var input = new DataBlob();
            var output = new DataBlob();
            IntPtr buffer = Marshal.AllocHGlobal(plain.Length);
            try
            {
                Marshal.Copy(plain, 0, buffer, plain.Length);
                input.CbData = plain.Length;
                input.PbData = buffer;
                if (!CryptProtectData(ref input, "CrimsonOnion", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, out output))
                    return plain;
                try
                {
                    var result = new byte[output.CbData];
                    Marshal.Copy(output.PbData, result, 0, output.CbData);
                    return result;
                }
                finally { LocalFree(output.PbData); }
            }
            catch
            {
                return plain;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static byte[] Unprotect(byte[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<byte>();
            var input = new DataBlob();
            var output = new DataBlob();
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                input.CbData = data.Length;
                input.PbData = buffer;
                if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, out output))
                    return data;
                try
                {
                    var result = new byte[output.CbData];
                    Marshal.Copy(output.PbData, result, 0, output.CbData);
                    return result;
                }
                finally { LocalFree(output.PbData); }
            }
            catch
            {
                return data;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
