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
            if(System.BitConverter.IsLittleEndian) {
                serials = System.BitConverter.ToInt32(wholeMessage, 0);
                type = (MessageType)System.BitConverter.ToInt32(wholeMessage, 4);
                extendVersion = System.BitConverter.ToInt32(wholeMessage, 8);
                extendLength = System.BitConverter.ToInt32(wholeMessage, 12);
            } else {
                byte[] reversed = new byte[12];
                for(var i = 0; i < 12; ++i) {
                    reversed[i] = wholeMessage[15 - i];
                }
                extendLength = System.BitConverter.ToInt32(wholeMessage, 0);
                extendVersion = System.BitConverter.ToInt32(wholeMessage, 4);
                type = (MessageType)System.BitConverter.ToInt32(wholeMessage, 8);
                serials = System.BitConverter.ToInt32(wholeMessage, 12);
            }
        }
    }

    public struct CustomMessage {
        public MessageBaseHead head;
        public long appid;
        public int contentLength;
        public int messageCode;
        public int conversationCode;
        public int conversationStepIndex;
        public ArmyAntMessage.System.ConversationStepType conversationStepType;
        public byte[] body;
    }
}
