using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EchoServer.Abstractions;

namespace EchoServer.Wrappers
{
    [ExcludeFromCodeCoverage]
    public class NetworkStreamWrapper : INetworkStream, IDisposable
    {
        private readonly NetworkStream _stream;
        private bool _disposed = false;

        public NetworkStreamWrapper(NetworkStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkStreamWrapper));
            return _stream.ReadAsync(buffer, offset, size, cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkStreamWrapper));
            return _stream.WriteAsync(buffer, offset, size, cancellationToken);
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _stream.Dispose();
                }

                // no unmanaged resources

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
