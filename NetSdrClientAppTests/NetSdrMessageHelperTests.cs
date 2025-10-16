using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessage_ShouldReturnValidMessage()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 100;

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            // Assert
            Assert.That(msg.Length, Is.GreaterThan(4));
            var headerBytes = msg.Take(2).ToArray();
            var codeBytes = msg.Skip(2).Take(2).ToArray();
            var parametersBytes = msg.Skip(4).ToArray();

            ushort num = BitConverter.ToUInt16(headerBytes);
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(actualLength, Is.EqualTo(msg.Length));
            Assert.That(BitConverter.ToUInt16(codeBytes), Is.EqualTo((ushort)code));
            Assert.That(parametersBytes.Length, Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessage_ShouldReturnValidMessageWithoutCode()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem1;
            int parametersLength = 50;

            // Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            // Assert
            Assert.That(msg.Length, Is.EqualTo(parametersLength + 2));
            var headerBytes = msg.Take(2).ToArray();
            ushort num = BitConverter.ToUInt16(headerBytes);
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);

            Assert.That(actualType, Is.EqualTo(type));
        }

        [Test]
        public void TranslateMessage_ShouldExtractCorrectData_ForControlItem()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ManualGain;
            var parameters = new byte[] { 0xAA, 0xBB, 0xCC };

            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var actualType, out var actualCode, out var seq, out var body);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(actualCode, Is.EqualTo(code));
            Assert.That(body, Is.EqualTo(parameters));
        }

        [Test]
        public void TranslateMessage_ShouldExtractCorrectData_ForDataItem()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            var parameters = new byte[] { 0x11, 0x22, 0x33, 0x44 };

            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, parameters);

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var actualType, out var actualCode, out var seq, out var body);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(actualCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(body, Is.EqualTo(parameters.Skip(2))); // 2 ·‡ÈÚË sequence
        }

        [Test]
        public void GetSamples_ShouldReturnCorrectIntegers()
        {
            // Arrange
            ushort sampleSize = 16; // 2 ·‡ÈÚË
            byte[] body = { 0x10, 0x00, 0x20, 0x00 }; // 0x0010, 0x0020

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            // Assert
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(0x0010));
            Assert.That(samples[1], Is.EqualTo(0x0020));
        }

        [Test]
        public void GetSamples_WithInvalidSampleSize_ShouldThrow()
        {
            // Arrange
            ushort sampleSize = 64;
            byte[] body = new byte[10];

            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => NetSdrMessageHelper.GetSamples(sampleSize, body).ToList());
        }

        [Test]
        public void GetMessage_ShouldThrow_WhenLengthTooLong()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var parameters = new byte[9000]; // too long

            // Act + Assert
            Assert.Throws<ArgumentException>(() =>
                typeof(NetSdrMessageHelper)
                .GetMethod("GetMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, new object[] { type, NetSdrMessageHelper.ControlItemCodes.None, parameters })
            );
        }

        [Test]
        public void TranslateHeader_ShouldHandleDataItemEdgeCase()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem0;
            byte[] header = BitConverter.GetBytes((ushort)((int)type << 13));

            // Act
            typeof(NetSdrMessageHelper)
                .GetMethod("TranslateHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, new object[] { header, null!, null! });

            // Just checking that it doesnít throw
            Assert.Pass("No exception thrown on edge case.");
        }
    }
}
