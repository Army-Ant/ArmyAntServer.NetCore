using System.Linq;
using System.Collections.Generic;

namespace ArmyAnt.Server {
    public enum MessageType : int {
        Unknown,
        Normal,
        File,
    }

    [System.Serializable]
    public struct MessageBaseHead {
        public int serials;
        public MessageType type;
        public int extendVersion;
        public int extendLength;
        public MessageBaseHead(byte[] wholeMessage) {
            serials = 0;
            type = MessageType.Normal;
            extendVersion = 1;
            extendLength = 1;
            Byte = wholeMessage;
        }
        public MessageBaseHead(int serials, MessageType type, int extendVersion, int extendLength) {
            this.serials = serials;
            this.type = type;
            this.extendVersion = extendVersion;
            this.extendLength = extendLength;
        }
        public byte[] Byte {
            get {
                var serialsBytes = System.BitConverter.GetBytes(serials);
                var typeBytes = System.BitConverter.GetBytes((int)type);
                var extendVersionBytes = System.BitConverter.GetBytes(extendVersion);
                var extendLengthBytes = System.BitConverter.GetBytes(extendLength);
                if(!System.BitConverter.IsLittleEndian) {
                    System.Array.Reverse(serialsBytes);
                    System.Array.Reverse(typeBytes);
                    System.Array.Reverse(extendVersionBytes);
                    System.Array.Reverse(extendLengthBytes);
                }
                var ret = new List<byte>();
                ret.AddRange(serialsBytes);
                ret.AddRange(typeBytes);
                ret.AddRange(extendVersionBytes);
                ret.AddRange(extendLengthBytes);
                return ret.ToArray();
            }
            set {
                if(System.BitConverter.IsLittleEndian) {
                    serials = System.BitConverter.ToInt32(value, 0);
                    type = (MessageType)System.BitConverter.ToInt32(value, 4);
                    extendVersion = System.BitConverter.ToInt32(value, 8);
                    extendLength = System.BitConverter.ToInt32(value, 12);
                } else {
                    byte[] reversed = new byte[12];
                    for(var i = 0; i < 12; ++i) {
                        reversed[i] = value[15 - i];
                    }
                    extendLength = System.BitConverter.ToInt32(value, 0);
                    extendVersion = System.BitConverter.ToInt32(value, 4);
                    type = (MessageType)System.BitConverter.ToInt32(value, 8);
                    serials = System.BitConverter.ToInt32(value, 12);
                }
            }
        }
        public static int GetNetworkMessageCode<T>(T msg) where T : Google.Protobuf.IMessage => GetNetworkMessageCode(msg.Descriptor);
        public static int GetNetworkMessageCode(Google.Protobuf.Reflection.MessageDescriptor msg) => msg.CustomOptions.TryGetInt32(50001, out int code) ? code : 0;
    }

    public struct CustomMessageSend<T> where T : Google.Protobuf.IMessage<T>, new() {
        public MessageBaseHead head;
        public long appid;
        public ArmyAntMessage.System.ConversationStepType conversationStepType;
        public T body;

        public static byte[] PackMessage(int conversationCode, int conversationStepIndex, CustomMessageSend<T> msg) {
            byte[] msg_byte = new byte[msg.body.CalculateSize()];
            var stream = new Google.Protobuf.CodedOutputStream(msg_byte);
            msg.body.WriteTo(stream);
            //var parser = new Google.Protobuf.MessageParser<T>(() => new T());
            //var reparse = parser.ParseFrom(msg_byte);
            switch(msg.head.extendVersion) {
                case 1:
                    var extend = new ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1();
                    extend.AppId = msg.appid;
                    extend.ConversationCode = conversationCode;
                    extend.ConversationStepType = msg.conversationStepType;
                    extend.ConversationStepIndex = conversationStepIndex;
                    extend.ContentLength = msg_byte.Length;
                    extend.MessageCode = MessageBaseHead.GetNetworkMessageCode(msg.body);
                    byte[] msg_extend = new byte[extend.CalculateSize()];
                    var extend_stream = new Google.Protobuf.CodedOutputStream(msg_extend);
                    msg.head.extendLength = msg_extend.Length;
                    extend.WriteTo(extend_stream);
                    var ret = new List<byte>();
                    ret.AddRange(msg.head.Byte);
                    ret.AddRange(msg_extend);
                    ret.AddRange(msg_byte);
                    return ret.ToArray();
            }
            return default;
        }
    }

    public struct CustomMessageReceived {
        public MessageBaseHead head;
        public long appid;
        public int contentLength;
        public int messageCode;
        public int conversationCode;
        public int conversationStepIndex;
        public ArmyAntMessage.System.ConversationStepType conversationStepType;
        public byte[] body;

        public static CustomMessageReceived ParseMessage(byte[] data) {
            var head = new MessageBaseHead(data);
            switch(head.extendVersion) {
                case 1:
                    var msg = ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1.Parser.ParseFrom(data, 16, head.extendLength);
                    return new CustomMessageReceived {
                        head = head,
                        appid = msg.AppId,
                        contentLength = msg.ContentLength,
                        messageCode = msg.MessageCode,
                        conversationCode = msg.ConversationCode,
                        conversationStepIndex = msg.ConversationStepIndex,
                        conversationStepType = msg.ConversationStepType,
                        body = data.Skip(16 + head.extendLength).ToArray(),
                    };
            }
            return default;
        }
    }
}
