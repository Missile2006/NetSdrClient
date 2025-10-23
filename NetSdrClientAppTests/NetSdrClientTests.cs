using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

[TestFixture]
public class NetSdrClientTests
{
    private NetSdrClient _client = null!;
    private Mock<ITcpClient> _tcpMock = null!;
    private Mock<IUdpClient> _udpMock = null!;

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()))
                .Returns(Task.CompletedTask);

        _udpMock = new Mock<IUdpClient>();
        _udpMock.Setup(udp => udp.StartListeningAsync())
                .Returns(Task.CompletedTask);

        _client = new NetSdrClient(_tcpMock.Object, _udpMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        // Act
        await _client.ConnectAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public void DisconnectWithNoConnectionTest()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _client.Disconect());
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        // Arrange 
        await _client.ConnectAsync();

        // Act
        _client.Disconect();

        // Assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {
        // Act & Assert
        await _client.StartIQAsync();

        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        // Arrange 
        await _client.ConnectAsync();

        // Act
        await _client.StartIQAsync();

        // Assert
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        // Arrange 
        await _client.ConnectAsync();
        await _client.StartIQAsync();

        // Act
        await _client.StopIQAsync();

        // Assert
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyAsync_SendsMessage()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        await _client.ChangeFrequencyAsync(144000000, 1);

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(4));
    }
}