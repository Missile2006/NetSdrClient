using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using EchoServer.Abstractions;
using Moq;
using NUnit.Framework;


namespace EchoServerTests
{
    [TestFixture]
    public class EchoServerTests
    {
        private Mock<ITcpListener> _mockListener;
        private Mock<ILogger> _mockLogger;
        private EchoServer.EchoServer _server;

        [SetUp]
        public void Setup()
        {
            _mockListener = new Mock<ITcpListener>();
            _mockLogger = new Mock<ILogger>();
            _server = new EchoServer.EchoServer(_mockListener.Object, _mockLogger.Object);
        }

        [Test]
        public async Task StartAsync_ShouldStartListenerAndAcceptClients()
        {
            var mockClient = new Mock<ITcpClient>();
            var mockStream = new Mock<INetworkStream>();
            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            _mockListener.SetupSequence(l => l.AcceptTcpClientAsync())
                         .ReturnsAsync(mockClient.Object)
                         .ThrowsAsync(new OperationCanceledException());

            await _server.StartAsync();

            _mockListener.Verify(l => l.Start(), Times.Once);
            _mockListener.Verify(l => l.AcceptTcpClientAsync(), Times.Exactly(2));
            _mockLogger.Verify(log => log.Log("Server started."), Times.Once);
            _mockLogger.Verify(log => log.Log("Server shutdown."), Times.Once);
        }

        [Test]
        public void Stop_ShouldStopListenerAndCancelToken()
        {
            _server.Stop();

            _mockListener.Verify(l => l.Stop(), Times.Once);
            _mockLogger.Verify(log => log.Log("Server stopped."), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldLogErrorAndCloseClient_WhenStreamThrowsException()
        {
            var mockClient = new Mock<ITcpClient>();
            var mockStream = new Mock<INetworkStream>();
            var exceptionMessage = "Connection was forcibly closed.";

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new IOException(exceptionMessage));

            await _server.HandleClientAsync(mockClient.Object, CancellationToken.None);

            mockStream.Verify(s => s.WriteAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockLogger.Verify(log => log.Log($"Error: {exceptionMessage}"), Times.Once);
            mockClient.Verify(c => c.Close(), Times.Once);
            _mockLogger.Verify(log => log.Log("Client disconnected."), Times.Once);
        }

        [Test]
        public async Task StartAsync_ShouldStopGracefully_WhenListenerThrowsObjectDisposedException()
        {
            _mockListener.Setup(l => l.AcceptTcpClientAsync()).ThrowsAsync(new ObjectDisposedException("TcpListener"));

            await _server.StartAsync();

            _mockListener.Verify(l => l.Start(), Times.Once);
            _mockLogger.Verify(log => log.Log("Server started."), Times.Once);
            _mockLogger.Verify(log => log.Log("Server shutdown."), Times.Once);
        }
    }
}