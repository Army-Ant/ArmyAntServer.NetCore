using System.Collections.Generic;
using System.Threading.Tasks;

using Google.Protobuf;
using Google.Protobuf.Reflection;

using ArmyAntMessage.System;

using ArmyAnt.ServerCore.Event;
using ArmyAnt.ServerCore.Main;


namespace ArmyAnt.ServerCore.SubUnit
{
    public delegate void MessageCallback(int conversationCode, EndPointTask user, IMessage data);

    public abstract class ASubUnit : ISubUnit
    {
        public ASubUnit(long appid, Server server, string loggerTag)
        {
            AppId = appid;
            Server = server;
            LoggerTag = loggerTag;
        }

        public long AppId { get; }

        public long TaskId { get; set; }

        public Server Server { get; }

        protected string LoggerTag { get; }

        public abstract void OnUserSessionDisconnected(long userId);

        public virtual void OnUserSessionLogin(long userId)
        {
        }

        public virtual void OnUserSessionLogout(long userId)
        {
        }

        public virtual void OnUserSessionReconnected(long userId)
        {
        }

        public virtual void OnUserSessionShutdown(long userId)
        {
            OnUserSessionDisconnected(userId);
        }

        public virtual bool Start()
        {
            return true;
        }

        public virtual bool Stop()
        {
            return true;
        }

        public virtual Task WaitAll()
        {
            return Server.EventManager.GetTask(TaskId);
        }

        public virtual void OnTask<Input>(int _event, params Input[] data)
        {
        }

        public void OnNetworkMessage(int code, SocketHeadExtend extend, IMessage data, EndPointTask user)
        {
            if (callbackList.ContainsKey(code))
            {
                callbackList[code](extend.ConversationCode, user, data);
            }
            else
            {
                Server.Log(IO.Logger.LogLevel.Warning, LoggerTag, "Received an unknown message, code:", code, ", user:", user.ID);
            }
        }

        protected void Log(IO.Logger.LogLevel level, params object[] content)
        {
            Server.Log(level, LoggerTag, content);
        }

        protected void RegisterMessage(MessageDescriptor descriptor, MessageCallback cb)
        {
            var code = descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            Server.RegisterMessage(descriptor);
            if (cb != null)
            {
                callbackList.Add(code, cb);
            }
        }

        private readonly IDictionary<int, MessageCallback> callbackList = new Dictionary<int, MessageCallback>();
    }
}
