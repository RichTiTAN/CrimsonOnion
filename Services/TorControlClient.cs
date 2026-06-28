using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrimsonOnion.Services
{
    public class TorControlClient : IDisposable
    {
        private readonly int _port;
        private readonly string _cookiePath;
        private readonly int _torIndex;
        public int TorIndex => _torIndex;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource _cts = new();

        public event Action<int, int>? BootstrapProgressUpdated;
        public event Action<int>? ConnectionDropped;

        public TorControlClient(int port, string cookiePath, int torIndex)
        {
            _port = port;
            _cookiePath = cookiePath;
            _torIndex = torIndex;
        }

        public void Start()
        {
            Task.Run(ConnectAndListenAsync);
        }

        private async Task ConnectAndListenAsync()
        {
            try
            {
                byte[] cookieBytes = Array.Empty<byte>();
                for (int i = 0; i < 40; i++)
                {
                    if (File.Exists(_cookiePath))
                    {
                        try
                        {
                            using var fs = new FileStream(_cookiePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            if (fs.Length == 32)
                            {
                                cookieBytes = new byte[32];
                                int bytesRead = await fs.ReadAsync(cookieBytes, 0, 32, _cts.Token);
                                if (bytesRead != 32) throw new Exception("Incomplete cookie read");
                                break;
                            }
                        }
                        catch { }
                    }
                    await Task.Delay(250, _cts.Token);
                }

                if (cookieBytes.Length != 32) return;

                string hexCookie = BitConverter.ToString(cookieBytes).Replace("-", "").ToUpperInvariant();

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        _client = new TcpClient();
                        await _client.ConnectAsync("127.0.0.1", _port, _cts.Token);
                        break;
                    }
                    catch
                    {
                        _client?.Dispose();
                        await Task.Delay(500, _cts.Token);
                    }
                }

                if (_client == null || !_client.Connected) return;

                _stream = _client.GetStream();
                _reader = new StreamReader(_stream, Encoding.ASCII);
                _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };

                await _writer.WriteLineAsync($"AUTHENTICATE {hexCookie}");
                string? authResp = await _reader.ReadLineAsync();
                if (authResp == null || !authResp.StartsWith("250")) return;

                await _writer.WriteLineAsync("SETEVENTS STATUS_CLIENT");
                string? subResp = await _reader.ReadLineAsync();
                if (subResp == null || !subResp.StartsWith("250")) return;

                await _writer.WriteLineAsync("GETINFO status/bootstrap-phase");

                while (!_cts.Token.IsCancellationRequested)
                {
                    string? line = await _reader.ReadLineAsync();
                    if (line == null)
                    {
                        if (!_cts.Token.IsCancellationRequested)
                            ConnectionDropped?.Invoke(_torIndex);
                        break;
                    }

                    if (line.Contains("BOOTSTRAP PROGRESS="))
                    {
                        int progIdx = line.IndexOf("BOOTSTRAP PROGRESS=") + 19;
                        int endIdx = line.IndexOf(' ', progIdx);
                        if (endIdx > progIdx)
                        {
                            if (int.TryParse(line.Substring(progIdx, endIdx - progIdx), out int pct))
                                BootstrapProgressUpdated?.Invoke(_torIndex, pct);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TorControlClient error for Tor{_torIndex}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            _reader?.Dispose();
            _writer?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
            _cts.Dispose();
        }
    }
}
