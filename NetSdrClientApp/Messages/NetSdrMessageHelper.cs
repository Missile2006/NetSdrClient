using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClientApp.Messages
{
    public static class NetSdrMessageHelper
    {
        private const short _maxMessageLength = 8191;
        private const short _maxDataItemMessageLength = 8194;
        private const short _msgHeaderLength = 2; //2 byte, 16 bit
        private const short _msgControlItemLength = 2; //2 byte, 16 bit
        private const short _msgSequenceNumberLength = 2; //2 byte, 16 bit

        public enum MsgTypes
        {
            SetControlItem,
            CurrentControlItem,
            ControlItemRange,
            Ack,
            DataItem0,
            DataItem1,
            DataItem2,
            DataItem3,
            GetControlItem
        }

        public enum ControlItemCodes
        {
            None = 0,
            IQOutputDataSampleRate = 0x00B8,
            RFFilter = 0x0044,
            ADModes = 0x008A,
            ReceiverState = 0x0018,
            ReceiverFrequency = 0x0020,
            ManualGain = 0xB9,
            DeviceStatus = 0xBA,
            Calibration = 0xBB,
            Reset = 0xBC
        }

        public static byte[] GetControlItemMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            return GetMessage(type, itemCode, parameters);
        }

        public static byte[] GetDataItemMessage(MsgTypes type, byte[] parameters)
        {
            return GetMessage(type, ControlItemCodes.None, parameters);
        }

        private static byte[] GetMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            var itemCodeBytes = Array.Empty<byte>();
            if (itemCode != ControlItemCodes.None)
            {
                itemCodeBytes = BitConverter.GetBytes((ushort)itemCode);
            }

            var headerBytes = GetHeader(type, itemCodeBytes.Length + parameters.Length);

            var msg = new List<byte>(headerBytes.Length + itemCodeBytes.Length + parameters.Length);
            msg.AddRange(headerBytes);
            msg.AddRange(itemCodeBytes);
            msg.AddRange(parameters);
            return msg.ToArray();
        }

        public static bool TranslateMessage(byte[] msg, out MsgTypes type, out ControlItemCodes itemCode, out ushort sequenceNumber, out byte[] body)
        {
            itemCode = ControlItemCodes.None;
            sequenceNumber = 0;
            bool success = true;

            var msgEnumerable = msg as IEnumerable<byte>;

            TranslateHeader(msgEnumerable.Take(_msgHeaderLength).ToArray(), out type, out int msgLength);
            msgEnumerable = msgEnumerable.Skip(_msgHeaderLength);
            msgLength -= _msgHeaderLength;

            if (type < MsgTypes.DataItem0) // get item code
            {
                var value = BitConverter.ToUInt16(msgEnumerable.Take(_msgControlItemLength).ToArray());
                msgEnumerable = msgEnumerable.Skip(_msgControlItemLength);
                msgLength -= _msgControlItemLength;
                if (Enum.IsDefined(typeof(ControlItemCodes), value))
                {
                    itemCode = (ControlItemCodes)value;
                }
                else
                {
                    success = false;
                }
            }
            else // get sequenceNumber
            {
                sequenceNumber = BitConverter.ToUInt16(msgEnumerable.Take(_msgSequenceNumberLength).ToArray());
                msgEnumerable = msgEnumerable.Skip(_msgSequenceNumberLength);
                msgLength -= _msgSequenceNumberLength;
            }

            body = msgEnumerable.ToArray();
            success &= body.Length == msgLength;
            return success;
        }

        // Public validation / entry point
        public static IEnumerable<int> GetSamples(ushort sampleSize, byte[] body)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));

            // convert bits to bytes (integer division as original)
            int bytesPerSample = sampleSize / 8;

            // allow only 1..4 bytes (8..32 bits)
            if (bytesPerSample < 1 || bytesPerSample > 4)
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(sampleSize),
                    actualValue: sampleSize,
                    message: "Sample size must be between 8 and 32 bits (i.e. converts to 1..4 bytes).");
            }

            return GetSamplesIterator(bytesPerSample, body);
        }

        // Private iterator - efficient, no LINQ Count/Skip/Take on IEnumerable
        private static IEnumerable<int> GetSamplesIterator(int bytesPerSample, byte[] body)
        {
            int offset = 0;
            int length = body.Length;
            byte[] buffer = new byte[4]; 

            while (offset + bytesPerSample <= length)
            {
                Array.Clear(buffer, 0, 4); 
                Array.Copy(body, offset, buffer, 0, bytesPerSample);
                yield return BitConverter.ToInt32(buffer, 0);
                offset += bytesPerSample;
            }
        }


        private static byte[] GetHeader(MsgTypes type, int msgLength)
        {
            int lengthWithHeader = msgLength + 2;
            //Data Items edge case
            if (type >= MsgTypes.DataItem0 && lengthWithHeader == _maxDataItemMessageLength)
            {
                lengthWithHeader = 0;
            }
            if (msgLength < 0 || lengthWithHeader > _maxMessageLength)
            {
                throw new ArgumentException("Message length exceeds allowed value");
            }
            return BitConverter.GetBytes((ushort)(lengthWithHeader + ((int)type << 13)));
        }

        private static void TranslateHeader(byte[] header, out MsgTypes type, out int msgLength)
        {
            var num = BitConverter.ToUInt16(header.ToArray());
            type = (MsgTypes)(num >> 13);
            msgLength = num - ((int)type << 13);
            if (type >= MsgTypes.DataItem0 && msgLength == 0)
            {
                msgLength = _maxDataItemMessageLength;
            }
        }
    }
}
