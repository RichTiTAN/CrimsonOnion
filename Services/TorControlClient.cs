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
        private bool _disposed = false;
        private Task? _listenerTask;

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
            _listenerTask = Task.Run(ConnectAndListenAsync)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        System.Diagnostics.Debug.WriteLine(
                            $"TorControlClient[{_torIndex}] listener faulted: {t.Exception?.InnerException?.Message}");
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task ConnectAndListenAsync()
        {
            try
            {
                byte[] cookieBytes = Array.Empty<byte>();
                for (int i = 0; i < 40; i++)
                {
                    if (_cts.Token.IsCancellationRequested) return;

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
                        catch (OperationCanceledException) { throw; }
                        catch { }
                    }
                    await Task.Delay(250, _cts.Token);
                }

                if (cookieBytes.Length != 32) return;

                string hexCookie = BitConverter.ToString(cookieBytes).Replace("-", "").ToUpperInvariant();

                TcpClient? connectedClient = null;
                for (int i = 0; i < 10; i++)
                {
                    if (_cts.Token.IsCancellationRequested) return;
                    TcpClient attempt = new TcpClient();
                    try
                    {
                        await attempt.ConnectAsync("127.0.0.1", _port, _cts.Token);
                        connectedClient = attempt;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        attempt.Dispose();
                        throw;
                    }
                    catch
                    {
                        attempt.Dispose();
                        await Task.Delay(500, _cts.Token);
                    }
                }

                if (connectedClient == null || !connectedClient.Connected) return;

                _client = connectedClient;
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
                string? getInfoResp;
                do
                {
                    getInfoResp = await _reader.ReadLineAsync();
                    if (getInfoResp == null) return;
                    TryParseBootstrapLine(getInfoResp);
                }
                while (!getInfoResp.StartsWith("250 "));

                while (!_cts.Token.IsCancellationRequested)
                {
                    string? line = await _reader.ReadLineAsync();
                    if (line == null)
                    {
                        if (!_cts.Token.IsCancellationRequested)
                            ConnectionDropped?.Invoke(_torIndex);
                        break;
                    }

                    TryParseBootstrapLine(line);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TorControlClient error for Tor{_torIndex}: {ex.Message}");
                if (!_cts.Token.IsCancellationRequested)
                    ConnectionDropped?.Invoke(_torIndex);
            }
            finally
            {
                _writer?.Dispose();
                _reader?.Dispose();
                _stream?.Dispose();
                _client?.Dispose();
                _writer = null;
                _reader = null;
                _stream = null;
                _client = null;
            }
        }

        private void TryParseBootstrapLine(string line)
        {
            if (!line.Contains("BOOTSTRAP PROGRESS=")) return;

            int progIdx = line.IndexOf("BOOTSTRAP PROGRESS=") + 19;
            int endIdx = line.IndexOf(' ', progIdx);
            if (endIdx < 0) endIdx = line.Length;

            if (endIdx > progIdx)
            {
                if (int.TryParse(line.Substring(progIdx, endIdx - progIdx), out int pct))
                    BootstrapProgressUpdated?.Invoke(_torIndex, pct);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;  
            _disposed = true;

            try { _cts.Cancel(); } catch { }
            _writer?.Dispose();
            _reader?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
            _cts.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
