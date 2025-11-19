using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2).ToArray();
            var codeBytes = msg.Skip(2).Take(2).ToArray();
            var parametersBytes = msg.Skip(4).ToArray();

            var num = BitConverter.ToUInt16(headerBytes, 0);
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes, 0);

            // Assert (group independent assertions)
            Assert.Multiple(() =>
            {
                Assert.That(headerBytes, Has.Length.EqualTo(2), "Header should contain 2 bytes.");
                Assert.That(codeBytes, Has.Length.EqualTo(2), "Code field should contain 2 bytes.");
                Assert.That(parametersBytes, Has.Length.EqualTo(parametersLength), "Parameters length mismatch.");

                Assert.That(msg.Length, Is.EqualTo(actualLength), "Message length in header should match actual message length.");
                Assert.That(type, Is.EqualTo(actualType), "Message type mismatch.");
                Assert.That(actualCode, Is.EqualTo((short)code), "Control item code mismatch.");
            });
        }


        [Test]
        public void GetDataItemMessageTest()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            // Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2).ToArray();
            var parametersBytes = msg.Skip(2).ToArray();

            var num = BitConverter.ToUInt16(headerBytes, 0);
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(headerBytes, Has.Length.EqualTo(2), "Header should contain 2 bytes.");
                Assert.That(parametersBytes, Has.Length.EqualTo(parametersLength), "Parameters length mismatch.");
                Assert.That(msg.Length, Is.EqualTo(actualLength), "Message length in header should match actual message length.");
                Assert.That(type, Is.EqualTo(actualType), "Message type mismatch.");
            });
        }

    }
}