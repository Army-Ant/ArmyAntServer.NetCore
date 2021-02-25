using System;
using System.Collections.Generic;

using ArmyAntMessage.System;

namespace ArmyAnt.MsgType
{

    [Serializable]
    public struct MessageBaseHead
    {
        public static readonly MessageBaseHead Default = new MessageBaseHead
        {
            type = MessageType.ProtocolBuffer,
            extendVersion = 1,
            extendLength = 1,
            contentLength = 1,
        };

        public MessageType type;
        public int extendVersion;
        public int extendLength;
        public int contentLength;

        public MessageBaseHead(byte[] wholeMessage)
        {
            type = MessageType.ProtocolBuffer;
            extendVersion = 1;
            extendLength = 1;
            contentLength = 1;
            Byte = wholeMessage;
        }

        public MessageBaseHead(MessageType type, int extendVersion, int extendLength, int contentLength)
        {
            this.type = type;
            this.extendVersion = extendVersion;
            this.extendLength = extendLength;
            this.contentLength = contentLength;
        }
        public byte[] Byte
        {
            get
            {
                var typeBytes = BitConverter.GetBytes((int)type);
                var extendVersionBytes = BitConverter.GetBytes(extendVersion);
                var extendLengthBytes = BitConverter.GetBytes(extendLength);
                var contentLengthBytes = BitConverter.GetBytes(contentLength);
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(typeBytes);
                    Array.Reverse(extendVersionBytes);
                    Array.Reverse(extendLengthBytes);
                    Array.Reverse(contentLengthBytes);
                }
                var ret = new List<byte>();
                ret.AddRange(typeBytes);
                ret.AddRange(extendVersionBytes);
                ret.AddRange(extendLengthBytes);
                ret.AddRange(contentLengthBytes);
                return ret.ToArray();
            }
            set
            {
                if (BitConverter.IsLittleEndian)
                {
                    type = (MessageType)BitConverter.ToInt32(value, 0);
                    extendVersion = BitConverter.ToInt32(value, 4);
                    extendLength = BitConverter.ToInt32(value, 8);
                    contentLength = BitConverter.ToInt32(value, 12);
                }
                else
                {
                    byte[] reversed = new byte[12];
                    for (var i = 0; i < 12; ++i)
                    {
                        reversed[i] = value[15 - i];
                    }
                    contentLength = BitConverter.ToInt32(value, 0);
                    extendLength = BitConverter.ToInt32(value, 4);
                    extendVersion = BitConverter.ToInt32(value, 8);
                    type = (MessageType)BitConverter.ToInt32(value, 12);
                }
            }
        }

        public static int GetNetworkMessageCode<T>(T msg) where T : Google.Protobuf.IMessage => GetNetworkMessageCode(msg.Descriptor);

        public static int GetNetworkMessageCode(Google.Protobuf.Reflection.MessageDescriptor msg) => msg.GetOptions().GetExtension(BaseExtensions.MsgCode);
    }
}
