using System;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer.Abstractions
{
    public interface INetworkStream : IDisposable
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken);
        Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken);
    }
}