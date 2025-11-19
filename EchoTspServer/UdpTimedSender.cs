using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace EchoServer
{
    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly UdpClient _udpClient;
        private Timer? _timer; // nullable
        private ushort _sequence = 0;
        private bool _disposed = false; // track disposal

        public UdpTimedSender(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _udpClient = new UdpClient();
        }

        public void StartSending(int intervalMilliseconds)
        {
            // throws ObjectDisposedException if _disposed == true
            ObjectDisposedException.ThrowIf(_disposed, nameof(UdpTimedSender));

            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");

            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        private void SendMessageCallback(object? state)
        {
            try
            {
                byte[] samples = new byte[1024];
                RandomNumberGenerator.Fill(samples);
                _sequence++;

                byte[] msg = (new byte[] { 0x04, 0x84 })
                    .Concat(BitConverter.GetBytes(_sequence))
                    .Concat(samples)
                    .ToArray();

                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);
                _udpClient.Send(msg, msg.Length, endpoint);

                Console.WriteLine($"Message sent to {_host}:{_port}, seq={_sequence}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public void StopSending()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }

        #region IDisposable Support
        [ExcludeFromCodeCoverage]
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    StopSending();
                    _udpClient.Dispose();
                }

                // No unmanaged resources to free here

                _disposed = true;
            }
        }
        [ExcludeFromCodeCoverage]
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
