using System;
using System.Collections.Generic;
using System.Text;

namespace ArmyAnt.ServerCore.MsgType
{
    public enum MessageType : int
    {
        Unknown,
        Protobuf,
        File,
        Json,
    }

    [System.Serializable]
    public struct MessageBaseHead
    {
        public int serials;
        public MessageType type;
        public int extendVersion;
        public int extendLength;
        public MessageBaseHead(byte[] wholeMessage)
        {
            serials = 0;
            type = MessageType.Protobuf;
            extendVersion = 1;
            extendLength = 1;
            Byte = wholeMessage;
        }
        public MessageBaseHead(int serials, MessageType type, int extendVersion, int extendLength)
        {
            this.serials = serials;
            this.type = type;
            this.extendVersion = extendVersion;
            this.extendLength = extendLength;
        }
        public byte[] Byte
        {
            get
            {
                var serialsBytes = BitConverter.GetBytes(serials);
                var typeBytes = BitConverter.GetBytes((int)type);
                var extendVersionBytes = BitConverter.GetBytes(extendVersion);
                var extendLengthBytes = BitConverter.GetBytes(extendLength);
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(serialsBytes);
                    Array.Reverse(typeBytes);
                    Array.Reverse(extendVersionBytes);
                    Array.Reverse(extendLengthBytes);
                }
                var ret = new List<byte>();
                ret.AddRange(serialsBytes);
                ret.AddRange(typeBytes);
                ret.AddRange(extendVersionBytes);
                ret.AddRange(extendLengthBytes);
                return ret.ToArray();
            }
            set
            {
                if (BitConverter.IsLittleEndian)
                {
                    serials = BitConverter.ToInt32(value, 0);
                    type = (MessageType)BitConverter.ToInt32(value, 4);
                    extendVersion = BitConverter.ToInt32(value, 8);
                    extendLength = BitConverter.ToInt32(value, 12);
                }
                else
                {
                    byte[] reversed = new byte[12];
                    for (var i = 0; i < 12; ++i)
                    {
                        reversed[i] = value[15 - i];
                    }
                    extendLength = BitConverter.ToInt32(value, 0);
                    extendVersion = BitConverter.ToInt32(value, 4);
                    type = (MessageType)BitConverter.ToInt32(value, 8);
                    serials = BitConverter.ToInt32(value, 12);
                }
            }
        }
        public static int GetNetworkMessageCode<T>(T msg) where T : Google.Protobuf.IMessage => GetNetworkMessageCode(msg.Descriptor);
        public static int GetNetworkMessageCode(Google.Protobuf.Reflection.MessageDescriptor msg) => msg.GetOptions().GetExtension(BaseExtensions.MsgCode);
    }
}
