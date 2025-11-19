using EchoServer.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EchoServer.Wrappers
{
    public class TcpListenerWrapper : ITcpListener
    {
        private readonly TcpListener _listener;

        public TcpListenerWrapper(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }

        public async Task<ITcpClient> AcceptTcpClientAsync()
        {
            var tcpClient = await _listener.AcceptTcpClientAsync();
            return new TcpClientWrapper(tcpClient);
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}