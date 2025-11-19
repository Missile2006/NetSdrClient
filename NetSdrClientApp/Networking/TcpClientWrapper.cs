using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    [ExcludeFromCodeCoverage]
    public class TcpClientWrapper : ITcpClient, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        public bool Connected => _tcpClient?.Connected == true && _stream != null;
        public event EventHandler<byte[]>? MessageReceived;
        private bool _disposed;

        public TcpClientWrapper(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
        }

        public void Connect()
        {
            if (Connected)
            {
                Console.WriteLine($"Already connected to {_host}:{_port}");
                return;
            }
            _tcpClient = new TcpClient();
            try
            {
                _cts = new CancellationTokenSource();
                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine($"Connected to {_host}:{_port}");
                _ = StartListeningAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to connect: {e.Message}");

                // Безпечне очищення ресурсів у разі помилки
                try { _cts?.Cancel(); }
                catch (Exception ex)
                {
                    // Можна ігнорувати, оскільки скасування токена не критичне
                    Console.WriteLine($"Ignored exception during CTS.Cancel: {ex.Message}");
                }

                try { _cts?.Dispose(); }
                catch (Exception ex)
                {
                    // Можна ігнорувати, оскільки Dispose безпечний
                    Console.WriteLine($"Ignored exception during CTS.Dispose: {ex.Message}");
                }
                _cts = null;

                try { _stream?.Dispose(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ignored exception during NetworkStream.Dispose: {ex.Message}");
                }
                _stream = null;

                try { _tcpClient?.Close(); _tcpClient?.Dispose(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ignored exception during TcpClient.Close/Dispose: {ex.Message}");
                }
                _tcpClient = null;
            }
        }

        public void Disconnect()
        {
            if (!Connected && _tcpClient == null && _stream == null && _cts == null)
            {
                Console.WriteLine("No active connection to disconnect.");
                return;
            }

            try { _cts?.Cancel(); }
            catch (Exception ex)
            {
                // Можна ігнорувати: токен скасовано або вже скасовано
                Console.WriteLine($"Ignored exception during CTS.Cancel: {ex.Message}");
            }

            try
            {
                _stream?.Close();
                _stream?.Dispose();
            }
            catch (Exception ex)
            {
                // Можна ігнорувати: стрім вже закритий
                Console.WriteLine($"Ignored exception during NetworkStream.Close/Dispose: {ex.Message}");
            }
            _stream = null;

            try
            {
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                // Можна ігнорувати: TcpClient вже закритий
                Console.WriteLine($"Ignored exception during TcpClient.Close/Dispose: {ex.Message}");
            }
            _tcpClient = null;

            try { _cts?.Dispose(); }
            catch (Exception ex)
            {
                // Можна ігнорувати: Dispose CTS безпечний
                Console.WriteLine($"Ignored exception during CTS.Dispose: {ex.Message}");
            }
            _cts = null;

            Console.WriteLine("Disconnected.");
        }

        public async Task SendMessageAsync(byte[] data)
        {
            await SendMessageInternalAsync(data).ConfigureAwait(false);
        }

        public async Task SendMessageAsync(string str)
        {
            await SendMessageInternalAsync(Encoding.UTF8.GetBytes(str)).ConfigureAwait(false);
        }

        private async Task SendMessageInternalAsync(byte[] data)
        {
            if (Connected && _stream != null && _stream.CanWrite)
            {
                var token = _cts?.Token ?? CancellationToken.None;
                Console.WriteLine($"Message sent: {BitConverter.ToString(data).Replace('-', ' ')}");
                await _stream.WriteAsync(data.AsMemory(), token).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException("Not connected to a server.");
            }
        }

        private async Task StartListeningAsync()
        {
            if (!Connected || _stream == null || !_stream.CanRead)
            {
                Console.WriteLine("Cannot start listener: not connected or stream not readable.");
                return;
            }
            var token = _cts?.Token ?? CancellationToken.None;
            try
            {
                Console.WriteLine("Starting listening for incoming messages.");
                while (!token.IsCancellationRequested)
                {
                    byte[] buffer = new byte[8194];
                    int bytesRead;
                    try
                    {
                        bytesRead = await _stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // При скасуванні токена вихід із циклу
                    }
                    catch (ObjectDisposedException)
                    {
                        break; // Стрім закрито, вихід із циклу
                    }
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Remote closed connection (bytesRead == 0).");
                        break;
                    }
                    MessageReceived?.Invoke(this, buffer.AsSpan(0, bytesRead).ToArray());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in listening loop: {e.Message}");
            }
            finally
            {
                Console.WriteLine("Listener stopped.");
                try { Disconnect(); }
                catch (Exception ex)
                {
                    // Можна ігнорувати: Disconnect обробляє винятки всередині
                    Console.WriteLine($"Ignored exception during Disconnect: {ex.Message}");
                }
            }
        }

        #region IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try { _cts?.Cancel(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ignored exception during CTS.Cancel: {ex.Message}");
                }
                try { _stream?.Dispose(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ignored exception during NetworkStream.Dispose: {ex.Message}");
                }
                try { _tcpClient?.Dispose(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ignored exception during TcpClient.Dispose: {ex.Message}");
                }
                try { _cts?.Dispose(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ignored exception during CTS.Dispose: {ex.Message}");
                }
                _stream = null;
                _tcpClient = null;
                _cts = null;
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
