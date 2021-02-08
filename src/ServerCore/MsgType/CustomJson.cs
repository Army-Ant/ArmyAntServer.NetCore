using System;
using System.Text;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;

using ArmyAntMessage.System;

namespace ArmyAnt.ServerCore.MsgType
{
    [Serializable]
    public struct CustomJsonData<T> where T : Google.Protobuf.IMessage<T>, new()
    {
        public long appid;
        public int messageCode;
        public ConversationStepType conversationStepType;
        public int conversationCode;
        public int conversationStepIndex;
        public T body;
    }

    public static class CustomJson
    {
        public static CustomJsonData<T> Deserialize<T>(byte[] data) where T : Google.Protobuf.IMessage<T>, new()
        {
            var stringReader = new System.IO.StringReader(BitConverter.ToString(data));
            var reader = new Newtonsoft.Json.JsonTextReader(stringReader);

            var serializer = Newtonsoft.Json.JsonSerializer.Create();
            return serializer.Deserialize<CustomJsonData<T>>(reader);
        }

        public static byte[] Serialize<T>(CustomJsonData<T> msg) where T : Google.Protobuf.IMessage<T>, new()
        {
            var stringWriter = new System.IO.StringWriter();
            var writer = new Newtonsoft.Json.JsonTextWriter(stringWriter);

            var serializer = Newtonsoft.Json.JsonSerializer.Create();
            serializer.Serialize(writer, msg);
            string ret = stringWriter.ToString();
            return Encoding.Default.GetBytes(ret);
        }

        public static bool CheckIsJsonFormat(byte[] data)
        {
            try
            {
                var reader = JsonReaderWriterFactory.CreateJsonReader(data, System.Xml.XmlDictionaryReaderQuotas.Max);
                var root = XElement.Load(reader);
                return root != null;
            }
            catch (Exception e)
            {
                var i = e.Message;
                return false;
            }
        }
    }
}

