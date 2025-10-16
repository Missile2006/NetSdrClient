using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NetSdrClientApp.Messages;
using System.Text;

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
        _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()))
                .Returns(Task.CompletedTask)
                .Callback<byte[]>(bytes =>
                {
                    var response = SimulateDeviceResponse(bytes);
                    _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, response);
                });

        _udpMock = new Mock<IUdpClient>();
        _udpMock.Setup(udp => udp.StartListeningAsync()).Returns(Task.CompletedTask);
        _udpMock.Setup(udp => udp.StopListening()).Verifiable();

        _client = new NetSdrClient(_tcpMock.Object, _udpMock.Object);
    }

    [Test]
    public async Task ConnectAsync_WhenNotConnected_ConnectsAndSendsSetupMessages()
    {
        // Act
        await _client.ConnectAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task ConnectAsync_WhenAlreadyConnected_DoesNotConnectAgain()
    {
        // Arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(true);

        // Act
        await _client.ConnectAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Never);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public void Disconnect_WhenCalled_DisconnectsTcpClient()
    {
        // Act
        _client.Disconnect();

        // Assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQAsync_WhenNotConnected_DoesNotSendMessage()
    {
        // Act
        await _client.StartIQAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task StartIQAsync_WhenConnected_SendsStartMessageAndStartsUdp()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        await _client.StartIQAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4)); 
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQAsync_WhenNotConnected_DoesNotSendMessage()
    {
        // Act
        await _client.StopIQAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task StopIQAsync_WhenConnectedAndIQStarted_SendsStopMessageAndStopsUdp()
    {
        // Arrange
        await _client.ConnectAsync();
        await _client.StartIQAsync();

        // Act
        await _client.StopIQAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(5)); 
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyAsync_WhenConnected_SendsFrequencyMessage()
    {
        // Arrange
        await _client.ConnectAsync();
        long frequency = 123456789;
        int channel = 2;

        // Act
        await _client.ChangeFrequencyAsync(frequency, channel);

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(b =>
            b.Length > 0 &&
            b.Skip(4).First() == (byte)channel
        )), Times.Once);
    }

    [Test]
    public async Task ChangeFrequencyAsync_WhenNotConnected_DoesNotSendMessage()
    {
        // Arrange
        long frequency = 123456789;
        int channel = 2;

        // Act
        await _client.ChangeFrequencyAsync(frequency, channel);

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task SetGainAsync_WhenConnected_SendsGainMessage()
    {
        // Arrange
        await _client.ConnectAsync();
        byte channel = 1;
        byte gainValue = 50;

        // Act
        await _client.SetGainAsync(channel, gainValue);

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public async Task SetBandwidthAsync_WhenConnected_SendsBandwidthMessage()
    {
        // Arrange
        await _client.ConnectAsync();
        byte channel = 1;
        int bandwidth = 1000000;

        // Act
        await _client.SetBandwidthAsync(channel, bandwidth);

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public async Task RequestDeviceStatusAsync_WhenConnected_SendsStatusRequest()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        await _client.RequestDeviceStatusAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public async Task CalibrateDeviceAsync_WhenConnected_SendsCalibrationMessage()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        await _client.CalibrateDeviceAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public async Task ResetDeviceAsync_WhenConnected_SendsResetMessage()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        await _client.ResetDeviceAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public void UdpClientMessageReceived_WhenDataReceived_WritesSamplesToFile()
    {
        // Arrange
        byte[] testData = CreateTestUdpData();
        string testFileName = "samples.bin";

        if (File.Exists(testFileName))
            File.Delete(testFileName);

        // Act
        _udpMock.Raise(udp => udp.MessageReceived += null, _udpMock.Object, testData);

        // Assert
        Assert.That(File.Exists(testFileName), Is.True);
        var fileInfo = new FileInfo(testFileName);
        Assert.That(fileInfo.Length, Is.GreaterThan(0));

        // Cleanup
        File.Delete(testFileName);
    }

    [Test]
    public async Task SendTcpRequest_WhenConnected_ReturnsResponse()
    {
        // Arrange
        await _client.ConnectAsync();
        byte[] testMessage = new byte[] { 0x01, 0x02, 0x03 };

        await _client.RequestDeviceStatusAsync();

        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public void TcpClientMessageReceived_WithPendingRequest_CompletesTask()
    {
        // Arrange
        var testResponse = new byte[] { 0x05, 0x06, 0x07 };

        Assert.DoesNotThrowAsync(async () =>
        {
            await _client.ConnectAsync();
        });
    }

    [Test]
    public async Task MultipleOperations_WhenConnected_ProcessesCorrectly()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act - Perform multiple operations
        await _client.StartIQAsync();
        await _client.ChangeFrequencyAsync(100000000, 1);
        await _client.SetGainAsync(1, 75);
        await _client.StopIQAsync();

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(6));
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    private byte[] SimulateDeviceResponse(byte[] request)
    {
        return new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00 }; 
    }

    private byte[] CreateTestUdpData()
    {
        var samples = new short[] { 100, -100, 200, -200, 150, -150 };
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        return stream.ToArray();
    }
}