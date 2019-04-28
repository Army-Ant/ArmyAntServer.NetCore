namespace ArmyAnt.Server {
    public interface IApplication {
        void Log(IO.Logger.LogLevel lv, string Tag, params object[] content);
        void Send<T>(Network.NetworkType type, long userId, int conversationStepIndex, CustomMessageSend<T> msg) where T : Google.Protobuf.IMessage;
    }
}
