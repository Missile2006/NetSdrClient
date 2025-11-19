using EchoServer.Abstractions;
using System.Net.Sockets;

namespace EchoServer.Wrappers
{
    public class TcpClientWrapper : ITcpClient
    {
        private readonly TcpClient _client;

        public TcpClientWrapper(TcpClient client)
        {
            _client = client;
        }

        public INetworkStream GetStream()
        {
            return new NetworkStreamWrapper(_client.GetStream());
        }

        public void Close()
        {
            _client.Close();
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}