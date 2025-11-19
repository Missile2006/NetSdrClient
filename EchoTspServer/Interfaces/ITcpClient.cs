using System;

namespace EchoServer.Abstractions
{
    public interface ITcpClient : IDisposable
    {
        INetworkStream GetStream();
        void Close();
    }
}