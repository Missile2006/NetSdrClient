using System.Threading.Tasks;

namespace EchoServer.Abstractions
{
    public interface ITcpListener
    {
        void Start();
        Task<ITcpClient> AcceptTcpClientAsync();
        void Stop();
    }
}