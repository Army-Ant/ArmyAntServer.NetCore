using System;
using System.Text;
using System.Collections.Generic;
using ArmyAnt.IO;
using ArmyAntMessage.System;
using ArmyAnt.MsgType;

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

    public class ConversationStatus
    {
        public int code;
        public MessageType type;
        public int stepIndex;
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
        public EndPointTask(Main.Server application, NetworkType clientType){
            app = application;
            NetworkType = clientType;
        }

        public MessageType msgType { get; set; }

        public NetworkType NetworkType { get; }

        public void OnLocalEvent<T>(int code, T data) {
        }

        public void OnNetworkMessage(int code, SocketHeadExtend extend, Google.Protobuf.IMessage data) {
            bool contain;
            bool stepEqual = false;
            lock(conversationWaitingList) {
                contain = conversationWaitingList.ContainsKey(extend.ConversationCode);
                if(contain) {
                    stepEqual = conversationWaitingList[extend.ConversationCode].stepIndex == extend.ConversationStepIndex;
                }
            }
            // Checking message step data
            switch(extend.ConversationStepType) {
                case ConversationStepType.NoticeOnly:
                    if(contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Notice should not has the same code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", extend.ConversationCode);
                        return;
                    } else if(extend.ConversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for notice, user: ", ID, " , message code: ", code, " , conversation step: ", extend.ConversationStepIndex);
                        return;
                    }
                    break;
                case ConversationStepType.AskFor:
                    if(contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Ask-for should not has the same code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", extend.ConversationCode);
                        return;
                    } else if(extend.ConversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for ask-for, user: ", ID, " , message code: ", code, " , conversation step: ", extend.ConversationStepIndex);
                        return;
                    } else {
                        // Record the ask-for
                        lock(conversationWaitingList) {
                            conversationWaitingList.Add(extend.ConversationCode, new ConversationStatus
                            {
                                code = extend.ConversationCode,
                                stepIndex = 0,
                            });
                        }
                    }
                    break;
                case ConversationStepType.StartConversation:
                    if(contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Conversation-start should not has the same code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", extend.ConversationCode);
                        return;
                    } else if(extend.ConversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-start, user: ", ID, " , message code: ", code, " , conversation step: ", extend.ConversationStepIndex);
                        return;
                    } else {
                        // Record the conversation
                        lock(conversationWaitingList) {
                            conversationWaitingList.Add(extend.ConversationCode, new ConversationStatus
                            {
                                code = extend.ConversationCode,
                                stepIndex = 1,
                            });
                        }
                    }
                    break;
                case ConversationStepType.ConversationStepOn:
                    if(!contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Conversation-step should has the past data code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", extend.ConversationCode);
                        return;
                    } else if(!stepEqual) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-step, user: ", ID, " , message code: ", code, " , conversation step: ", extend.ConversationStepIndex);
                        return;
                    } else {
                        // Record the conversation step
                        lock(conversationWaitingList) {
                            conversationWaitingList[extend.ConversationCode].stepIndex = 1 + conversationWaitingList[extend.ConversationCode].stepIndex;
                        }
                    }
                    break;
                case ConversationStepType.ResponseEnd:
                    if(!contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "The end should has the past data code in waiting list, user: ", ID, " , message code: ", code, " , conversation code: ", extend.ConversationCode);
                        return;
                    } else if(stepEqual && extend.ConversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for end, user: ", ID, " , message code: ", code, " , conversation step: ", extend.ConversationStepIndex);
                        return;
                    } else {
                        // Remove the waiting data
                        lock(conversationWaitingList) {
                            conversationWaitingList.Remove(extend.ConversationCode);
                        }
                    }
                    break;
                default:
                    app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Network message unknown type number, user: ", ID, " , message code: ", code, " , conversation type: ", (long)extend.ConversationStepType);
                    return;
            }

            var subApp = app.GetSubApplication(extend.AppId);
            subApp?.OnNetworkMessage(code, extend, data, this);
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
            else if (data[0] is SocketHeadExtend extend && data.Length == 2 && data[1] is Google.Protobuf.IMessage net)
            {

                OnNetworkMessage(_event, extend, net);
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

        public bool SendMessage<T>(long appId, ConversationStepType stepType, T msg, int conversationCode) where T : Google.Protobuf.IMessage<T>, new() {
            // 这是从 C++ 的 ArmyAntServer 复制过来的发送代码, 因为时间来不及, 先放在这里提交, 回去处理
            int conversationStepIndex = 0;
            bool contains = false;
            lock(conversationWaitingList) {
                {
                    contains = conversationWaitingList.ContainsKey(conversationCode);
                    if(contains) {
                        conversationStepIndex = conversationWaitingList[conversationCode].stepIndex;
                    }
                }
            }
            switch(stepType) {
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
                    app.Log(Logger.LogLevel.Error, LOGGER_TAG, "Unknown conversation step type when sending a network message: ", stepType);
                    return false;
            }

            lock(conversationWaitingList) {
                switch(stepType) {
                    case ConversationStepType.AskFor:
                        conversationWaitingList.Add(conversationCode, new ConversationStatus
                        {
                            code = conversationCode,
                            stepIndex = 0,
                        });
                        break;
                    case ConversationStepType.StartConversation:
                        conversationWaitingList.Add(conversationCode, new ConversationStatus
                        {
                            code = conversationCode,
                            stepIndex = 1,
                        });
                        break;
                    case ConversationStepType.ConversationStepOn:
                        conversationWaitingList[conversationCode].stepIndex = conversationStepIndex;
                        break;
                    case ConversationStepType.ResponseEnd:
                        conversationWaitingList.Remove(conversationCode);
                        break;
                    default:
                        // TODO: Warning
                        break;
                }
            }
            var extend = new SocketHeadExtend();
            extend.ConversationCode = conversationCode;
            extend.ConversationStepIndex = conversationStepIndex;
            extend.ConversationStepType = stepType;
            extend.AppId = appId;
            extend.MessageCode = MessageBaseHead.GetNetworkMessageCode(msg);
            app.Send(msgType, NetworkType, ID, extend, msg);
            return true;
        }

        private Main.Server app;

        private IDictionary<int, ConversationStatus> conversationWaitingList = new Dictionary<int, ConversationStatus>();

        private const string LOGGER_TAG = "Main EndPointTask Session";
    }
}
