using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EchoServer;
using NUnit.Framework;

namespace EchoServerTests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        private const int ReceiveTimeoutMs = 2000;
        private UdpClient? _listener;
        private int _port;
        private UdpTimedSender? _sender;

        [SetUp]
        public void SetUp()
        {
            _listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); // ephemeral port
            _port = ((IPEndPoint)_listener.Client.LocalEndPoint!).Port;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _sender?.StopSending();
                _sender?.Dispose();
            }
            catch
            {
                // ignore cleanup exceptions
            }

            try
            {
                _listener?.Close();
                _listener?.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        private static async Task<UdpReceiveResult?> ReceiveWithTimeoutAsync(UdpClient listener, int timeoutMs)
        {
            var receiveTask = listener.ReceiveAsync();
            var delayTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(receiveTask, delayTask).ConfigureAwait(false);
            if (completed == receiveTask)
            {
                return receiveTask.Result;
            }
            return null;
        }

        [Test]
        public async Task StartSending_SendsUdpMessage_WithExpectedFormat()
        {
            // Arrange
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                // Act
                _sender.StartSending(50); // small interval
                var received = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);

                // Assert (group independent assertions)
                Assert.Multiple(() =>
                {
                    Assert.That(received, Is.Not.Null, "No UDP message received within timeout.");

                    var data = received!.Value.Buffer;

                    // expected minimum size: 2(header) + 2(seq) + payload (>=1)
                    Assert.That(data, Has.Length.GreaterThanOrEqualTo(2 + 2 + 1), "Received data too short.");

                    Assert.That(data[0], Is.EqualTo(0x04), "First header byte mismatch.");
                    Assert.That(data[1], Is.EqualTo(0x84), "Second header byte mismatch.");

                    ushort seq = BitConverter.ToUInt16(data, 2);
                    // first message should have seq == 1
                    Assert.That(seq, Is.EqualTo((ushort)1), "Sequence number of first message should be 1.");

                    // Expected total >= 1028 bytes
                    Assert.That(data, Has.Length.GreaterThanOrEqualTo(1028), "Expected message length at least 1028 bytes.");
                });
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }



        [Test]
        public void StartSending_Throws_WhenAlreadyRunning()
        {
            // Arrange
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                // Act
                _sender.StartSending(100);

                // Assert
                Assert.Multiple(() =>
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => _sender!.StartSending(100));

                    Assert.That(ex, Is.Not.Null, "Expected InvalidOperationException but got null.");
                    Assert.That(
                        ex!.Message.IndexOf("already running", StringComparison.OrdinalIgnoreCase),
                        Is.GreaterThanOrEqualTo(0),
                        "Exception message does not contain expected text 'already running'."
                    );
                });
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }



        [Test]
        public async Task StopSending_StopsFurtherMessages()
        {
            // Arrange
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                _sender.StartSending(50);

                // receive at least one message
                var first = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
                Assert.That(first, Is.Not.Null, "Expected to receive at least one message after start.");

                // Now stop the sender
                _sender.StopSending();

                // Try to receive another message within a short time - should time out (no more sends)
                var second = await ReceiveWithTimeoutAsync(_listener!, 500);
                Assert.That(second, Is.Null, "No further messages expected after StopSending.");
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }

        [Test]
        public async Task Dispose_StopsAndDisposesResources_NoExceptions()
        {
            // Arrange
            _sender = new UdpTimedSender("127.0.0.1", _port);

            // Act
            _sender.StartSending(50);

            // give it a little time to send something
            var received = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
            Assert.That(received, Is.Not.Null, "Expected message before dispose.");

            // Dispose should stop sending and not throw
            Assert.DoesNotThrow(() => _sender!.Dispose());

            // After dispose there should be no more messages; try receive short timeout
            var afterDispose = await ReceiveWithTimeoutAsync(_listener!, 300);
            Assert.That(afterDispose, Is.Null, "No messages expected after Dispose.");
            _sender = null; // already disposed
        }

        [Test]
        public async Task Messages_Sequence_IncrementsAcrossSends()
        {
            // Arrange
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                _sender.StartSending(50);

                // Receive first two messages
                var first = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
                Assert.That(first, Is.Not.Null, "First message not received.");
                var firstSeq = BitConverter.ToUInt16(first!.Value.Buffer, 2);

                var second = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
                Assert.That(second, Is.Not.Null, "Second message not received.");
                var secondSeq = BitConverter.ToUInt16(second!.Value.Buffer, 2);

                Assert.That(secondSeq, Is.EqualTo((ushort)(firstSeq + 1)), "Sequence should increment by 1.");
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }
    }
}