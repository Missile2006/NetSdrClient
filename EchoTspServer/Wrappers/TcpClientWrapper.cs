using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using EchoServer.Abstractions;

namespace EchoServer.Wrappers
{
    public class TcpClientWrapper : ITcpClient
    {
        private readonly TcpClient _client;
        private bool _disposed = false;

        public TcpClientWrapper(TcpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public INetworkStream GetStream()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientWrapper));
            return new NetworkStreamWrapper(_client.GetStream());
        }

        public void Close()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientWrapper));
            _client.Close();
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
                    _client.Dispose();
                }
                // no unmanaged resources
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
