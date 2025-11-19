using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;
using EchoServer;
using EchoServer.Wrappers;

namespace EchoServer
{
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        public static Task Main(string[] args)
        {
            int serverPort = 5000;
            var logger = new ConsoleLogger();
            var listener = new TcpListenerWrapper(IPAddress.Any, serverPort);

            var server = new EchoServer(listener, logger);

            _ = Task.Run(() => server.StartAsync());

            string host = "127.0.0.1";
            int udpPort = 60000;
            int intervalMilliseconds = 5000;

            using (var sender = new UdpTimedSender(host, udpPort))
            {
                logger.Log("Press 'q' to stop the server and sender...");
                sender.StartSending(intervalMilliseconds);

                while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
                {
                    // Just wait until 'q' is pressed
                }

                sender.StopSending();
                server.Stop();
                logger.Log("Sender and server stopped.");
            }

            return Task.CompletedTask;
        }
    }
}