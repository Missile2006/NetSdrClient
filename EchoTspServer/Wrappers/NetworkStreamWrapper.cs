using EchoServer.Abstractions;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer.Wrappers
{
    public class NetworkStreamWrapper : INetworkStream
    {
        private readonly NetworkStream _stream;

        public NetworkStreamWrapper(NetworkStream stream)
        {
            _stream = stream;
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            return _stream.ReadAsync(buffer, offset, size, cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            return _stream.WriteAsync(buffer, offset, size, cancellationToken);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}