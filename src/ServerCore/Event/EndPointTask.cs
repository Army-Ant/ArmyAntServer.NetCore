using System;
using System.Text;
using System.Collections.Generic;
using ArmyAnt.IO;
using ArmyAntMessage.System;
using ArmyAnt.ServerCore.MsgType;

namespace ArmyAnt.ServerCore.Event
{
    #region delegates
    public delegate void OnUserSessionLogin(long index);
    public delegate void OnUserSessionLogout(long index);
    public delegate void OnUserSessionShutdown(long index);
    public delegate void OnUserSessionDisconnected(long index);
    public delegate void OnUserSessionReconnected(long index);
    #endregion

    public class LocalEventArg : EventArgs
    {
        public int code;
    }

    public enum SessionStatus
    {
        UnInitialized,
        Online,     // Logged in or reconnected
        Busy,
        Disconnected,   // Disconnected and waiting for reconnection
        Shutdowned, // Disconnected and no longer waiting for reconnection
        Offline,    // Logged out
    }

    public class EndPointTask : Thread.TaskPool<int>.ITaskQueue
    {
        public EndPointTask(Main.Server application, Network.NetworkType clientType){
            app = application;
            NetworkType = clientType;
        }

        public Network.NetworkType NetworkType { get; }

        public void OnLocalEvent<T>(int code, T data) {
        }

        public void OnNetworkMessage(int code, CustomMessageReceived data) {
            bool contain;
            bool stepEqual = false;
            lock(conversationWaitingList) {
                contain = conversationWaitingList.ContainsKey(data.conversationCode);
                if(contain) {
                    stepEqual = conversationWaitingList[data.conversationCode] == data.conversationStepIndex;
                }
            }
            // Checking message step data
            switch(data.conversationStepType) {
                case ConversationStepType.NoticeOnly:
                    if(contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Notice should not has the same code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for notice, user: ", ID, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    }
                    break;
                case ConversationStepType.AskFor:
                    if(contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Ask-for should not has the same code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for ask-for, user: ", ID, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Record the ask-for
                        lock(conversationWaitingList) {
                            conversationWaitingList.Add(data.conversationCode, 0);
                        }
                    }
                    break;
                case ConversationStepType.StartConversation:
                    if(contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Conversation-start should not has the same code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-start, user: ", ID, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Record the conversation
                        lock(conversationWaitingList) {
                            conversationWaitingList.Add(data.conversationCode, 1);
                        }
                    }
                    break;
                case ConversationStepType.ConversationStepOn:
                    if(!contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Conversation-step should has the past data code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(!stepEqual) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-step, user: ", ID, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Record the conversation step
                        lock(conversationWaitingList) {
                            conversationWaitingList[data.conversationCode] = 1 + conversationWaitingList[data.conversationCode];
                        }
                    }
                    break;
                case ConversationStepType.ResponseEnd:
                    if(!contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "The end should has the past data code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(stepEqual && data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for end, user: ", ID, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Remove the waiting data
                        lock(conversationWaitingList) {
                            conversationWaitingList.Remove(data.conversationCode);
                        }
                    }
                    break;
                default:
                    app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Network message unknown type number, user: ", ID, " , message code: ", code, " , conversation type: ", (long)data.conversationStepType);
                    return;
            }

            var subApp = app.GetSubApplication(data.appid);
            subApp?.OnNetworkMessage(code, data, this);
        }

        public void OnUnknownEvent<T>(int _event, params T[] data) {
        }

        public void OnUserSessionDisconnected(long index) {
        }

        public void OnUserSessionLogin(long index) {
        }

        public void OnUserSessionLogout(long index) {
        }

        public void OnUserSessionReconnected(long index) {
        }

        public void OnUserSessionShutdown(long index) {
        }

        public int GenerateNewConversationCode() {
            int ret = 0;
            lock(conversationWaitingList) {
                while(conversationWaitingList.ContainsKey(++ret)) { }
            }
            return ret;
        }

        public long ID { get; set; }
        public SessionStatus SessionStatus { get; private set; }


        public void OnTask<Input>(int _event, params Input[] data)
        {
            if (data[0] is LocalEventArg local)
            {
                OnLocalEvent(_event, local);
            }
            else if (data[0] is CustomMessageReceived net)
            {
                OnNetworkMessage(_event, net);
            }
            else if (data[0] is int user && data.Length == 2 && data[1] is int check && check == 0)
            {
                switch ((SpecialEvent)_event)
                {
                    case SpecialEvent.UserLogin:
                        SessionStatus = SessionStatus.Online;
                        OnUserSessionLogin(user);
                        break;
                    case SpecialEvent.UserLogout:
                        SessionStatus = SessionStatus.Offline;
                        OnUserSessionLogout(user);
                        break;
                    case SpecialEvent.UserShutdown:
                        SessionStatus = SessionStatus.Shutdowned;
                        OnUserSessionShutdown(user);
                        break;
                    case SpecialEvent.UserDisconnected:
                        SessionStatus = SessionStatus.Disconnected;
                        OnUserSessionDisconnected(user);
                        break;
                    case SpecialEvent.UserReconnected:
                        SessionStatus = SessionStatus.Online;
                        OnUserSessionReconnected(user);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                OnUnknownEvent(_event, data);
            }
        }

        public bool SendMessage<T>(CustomMessageSend<T> msg, int conversationCode = 0) where T : Google.Protobuf.IMessage<T>, new() {
            // 这是从 C++ 的 ArmyAntServer 复制过来的发送代码, 因为时间来不及, 先放在这里提交, 回去处理
            int conversationStepIndex = 0;
            bool contains = false;
            lock(conversationWaitingList) {
                {
                    contains = conversationWaitingList.ContainsKey(conversationCode);
                    if(contains) {
                        conversationStepIndex = conversationWaitingList[conversationCode];
                    }
                }
            }
            switch(msg.conversationStepType) {
                case ConversationStepType.NoticeOnly:
                case ConversationStepType.AskFor:
                case ConversationStepType.StartConversation:
                    conversationStepIndex = 0;
                    if(contains) {
                        app.Log(Logger.LogLevel.Error, LOGGER_TAG, "Sending a network message as conversation start with an existed code: ", conversationCode);
                        return false;
                }
                    break;
                case ConversationStepType.ConversationStepOn:
                    if(!contains) {
                        app.Log(Logger.LogLevel.Error, LOGGER_TAG, "Sending a network message as conversation step on with an inexisted code: ", conversationCode);
                        return false;
                    }
                    conversationStepIndex += 1;
                    break;
                case ConversationStepType.ResponseEnd:
                    if(!contains) {
                        app.Log(Logger.LogLevel.Error, LOGGER_TAG, "Sending a network message as conversation reply with an unexisted code: ", conversationCode);
                        return false;
                    }
                    conversationStepIndex += 1;
                    break;
                default:
                    app.Log(Logger.LogLevel.Error, LOGGER_TAG, "Unknown conversation step type when sending a network message: ", msg.conversationStepType);
                    return false;
            }

            lock(conversationWaitingList) {
                switch(msg.conversationStepType) {
                    case ConversationStepType.AskFor:
                        conversationWaitingList.Add(conversationCode, 0);
                        break;
                    case ConversationStepType.StartConversation:
                        conversationWaitingList.Add(conversationCode, 1);
                        break;
                    case ConversationStepType.ConversationStepOn:
                        conversationWaitingList[conversationCode] = conversationStepIndex;
                        break;
                    case ConversationStepType.ResponseEnd:
                        conversationWaitingList.Remove(conversationCode);
                        break;
                    default:
                        // TODO: Warning
                        break;
                }
            }

            app.Send(NetworkType, ID, conversationCode, conversationStepIndex, msg);
            return true;
        }

        private Main.Server app;

        private IDictionary<int, int> conversationWaitingList = new Dictionary<int, int>();

        private const string LOGGER_TAG = "Main EndPointTask Session";
    }
}
