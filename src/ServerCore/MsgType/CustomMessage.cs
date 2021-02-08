using System.Linq;
using System.Collections.Generic;

using ArmyAntMessage.System;

namespace ArmyAnt.ServerCore.MsgType
{
    public struct CustomMessageSend<T> where T : Google.Protobuf.IMessage<T>, new()
    {
        public MessageBaseHead head;
        public long appid;
        public ConversationStepType conversationStepType;
        public T body;

        public static byte[] PackMessage(int conversationCode, int conversationStepIndex, CustomMessageSend<T> msg)
        {
            byte[] msg_byte = new byte[msg.body.CalculateSize()];
            var stream = new Google.Protobuf.CodedOutputStream(msg_byte);
            msg.body.WriteTo(stream);
            switch (msg.head.extendVersion)
            {
                case 1:
                    var extend = new SocketExtendNormal_V0_0_0_1
                    {
                        AppId = msg.appid,
                        ConversationCode = conversationCode,
                        ConversationStepType = msg.conversationStepType,
                        ConversationStepIndex = conversationStepIndex,
                        ContentLength = msg_byte.Length,
                        MessageCode = MessageBaseHead.GetNetworkMessageCode(msg.body)
                    };
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
        public ConversationStepType conversationStepType;

        public int contentLength;
        public int messageCode;
        public int conversationCode;
        public int conversationStepIndex;
        public byte[] body;

        public static CustomMessageReceived ParseMessage(byte[] data) {
            var head = new MessageBaseHead(data);
            switch(head.extendVersion) {
                case 1:
                    var msg = SocketExtendNormal_V0_0_0_1.Parser.ParseFrom(data, 16, head.extendLength);
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
