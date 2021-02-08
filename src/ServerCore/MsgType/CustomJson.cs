using System;
using System.Text;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;

using ArmyAntMessage.System;

namespace ArmyAnt.ServerCore.MsgType
{
    [Serializable]
    public class CustomData
    {
        public long appid;
        public int messageCode;
        public ConversationStepType conversationStepType;
        public int conversationCode;
        public int conversationStepIndex;
    }

    [Serializable]
    public class CustomJsonData<T> : CustomData where T : Google.Protobuf.IMessage<T>, new()
    {
        public T body;
    }

    public static class CustomJson
    {
        public static (CustomData, string) DeserializeBase(byte[] data)
        {
            var stringReader = new System.IO.StringReader(BitConverter.ToString(data));
            var reader = new Newtonsoft.Json.JsonTextReader(stringReader);

            var serializer = Newtonsoft.Json.JsonSerializer.Create();
            try
            {
                return (serializer.Deserialize<CustomData>(reader), stringReader.ReadToEnd());
            }catch(Exception e)
            {
                return (null, null);
            }
        }

        public static CustomJsonData<T> Deserialize<T>(byte[] data) where T : Google.Protobuf.IMessage<T>, new()
        {
            return Deserialize<T>(BitConverter.ToString(data));
        }

        public static CustomJsonData<T> Deserialize<T>(string json) where T : Google.Protobuf.IMessage<T>, new()
        {
            var stringReader = new System.IO.StringReader(json);
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
    }
}

