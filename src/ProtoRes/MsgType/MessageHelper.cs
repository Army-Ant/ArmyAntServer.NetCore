using System.Collections.Generic;
using System.Linq;
using System.Text;

using ArmyAntMessage.System;

using Google.Protobuf;

using Newtonsoft.Json.Linq;

namespace ArmyAnt.MsgType
{
    public class MessageHelper
    {
        public MessageHelper()
        {
            JsonParser.Settings.Default.IgnoreUnknownFields = true;
            JsonFormatter.Settings.Default.FormatDefaultValues = true;
        }

        public void RegisterMessage(Google.Protobuf.Reflection.MessageDescriptor descriptor)
        {
            var code = descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            messageTypeDic[code] = descriptor;
        }

        public byte[] SerializeBinary(SocketHeadExtend extend, IMessage msg)
        {
            byte[] msg_byte = new byte[msg.CalculateSize()];
            var stream = new CodedOutputStream(msg_byte);
            msg.WriteTo(stream);

            byte[] msg_extend = new byte[extend.CalculateSize()];
            var extend_stream = new CodedOutputStream(msg_extend);
            extend.WriteTo(extend_stream);

            var head = new MessageBaseHead
            {
                type = MessageType.Protobuf,
                extendVersion = 1,
                extendLength = msg_extend.Length,
                contentLength = msg_byte.Length,
            };

            var ret = new List<byte>();
            ret.AddRange(head.Byte);
            ret.AddRange(msg_extend);
            ret.AddRange(msg_byte);
            return ret.ToArray();
        }

        public string SerializeJson(SocketHeadExtend extend, IMessage msg)
        {
            var jsonExtend = extend.ToString();
            var jsonMsg = msg.ToString();
            var jObjExtend = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonExtend) as JObject;
            var jObjMsg = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonMsg) as JObject;
            jObjExtend.Merge(jObjMsg);
            return jObjExtend.ToString();
        }

        public (SocketHeadExtend extend, IMessage msg, MessageType msgType) Deserialize(bool allowJson, byte[] data)
        {
            SocketHeadExtend extend = null;
            IMessage msg = null;
            MessageType msgType = MessageType.Protobuf;
            if (allowJson)
            {
                try
                {
                    var str = Encoding.Default.GetString(data);
                    (extend, msg) = DeserializeJson(str);
                    if (extend != null && extend.ConversationStepType != ConversationStepType.Default)
                    {
                        msgType = MessageType.Json;
                    }
                }
                catch (System.Exception e)
                {

                }
            }
            if (msgType != MessageType.Json)
            {
                MessageBaseHead head;
                (head, extend, msg) = DeserializeBinary(data);
            }
            return (extend, msg, msgType);
        }

        private (MessageBaseHead head, SocketHeadExtend extend, IMessage msg) DeserializeBinary(byte[] data)
        {
            var head = new MessageBaseHead(data);
            var extend = SocketHeadExtend.Parser.ParseFrom(data, 16, head.extendLength);
            var msg = messageTypeDic[extend.MessageCode].Parser.ParseFrom(data.Skip(16 + head.extendLength).ToArray());
            return (head, extend, msg);
        }

        private (SocketHeadExtend extend, IMessage msg) DeserializeJson(string json)
        {
            var extend = SocketHeadExtend.Parser.ParseJson(json);
            var msg = messageTypeDic[extend.MessageCode].Parser.ParseJson(json);
            return (extend, msg);
        }

        private readonly IDictionary<int, Google.Protobuf.Reflection.MessageDescriptor> messageTypeDic = new Dictionary<int, Google.Protobuf.Reflection.MessageDescriptor>();

    }
}
