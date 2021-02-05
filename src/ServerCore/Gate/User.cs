using System;
using System.Text;
using System.Collections.Generic;
using ArmyAnt.IO;
using ArmyAntMessage.System;

namespace ArmyAnt.Server.Gate {
    public class User : Event.AUserSession {
        public User(Application application, Network.NetworkType clientType){
            app = application;
            NetworkType = clientType;
        }

        public Network.NetworkType NetworkType { get; }

        public override void OnLocalEvent<T>(int code, T data) {
        }

        public override void OnNetworkMessage(int code, CustomMessageReceived data) {
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
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Notice should not has the same code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for notice, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    }
                    break;
                case ConversationStepType.AskFor:
                    if(contain) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Ask-for should not has the same code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for ask-for, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
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
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Conversation-start should not has the same code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-start, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
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
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Conversation-step should has the past data code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(!stepEqual) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-step, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
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
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "The end should has the past data code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(stepEqual && data.conversationStepIndex != 0) {
                        app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for end, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Remove the waiting data
                        lock(conversationWaitingList) {
                            conversationWaitingList.Remove(data.conversationCode);
                        }
                    }
                    break;
                default:
                    app.Log(Logger.LogLevel.Warning, LOGGER_TAG, "Network message unknown type number, user: ", UserId, " , message code: ", code, " , conversation type: ", (long)data.conversationStepType);
                    return;
            }

            var subApp = app.GetSubApplication(data.appid);
            subApp?.OnNetworkMessage(code, data, this);
        }

        public override void OnUnknownEvent<T>(int _event, params T[] data) {
        }

        public override void OnUserSessionDisconnected(long index) {
        }

        public override void OnUserSessionLogin(long index) {
        }

        public override void OnUserSessionLogout(long index) {
        }

        public override void OnUserSessionReconnected(long index) {
        }

        public override void OnUserSessionShutdown(long index) {
        }

        public int GenerateNewConversationCode() {
            int ret = 0;
            lock(conversationWaitingList) {
                while(conversationWaitingList.ContainsKey(++ret)) { }
            }
            return ret;
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

            app.Send(NetworkType, UserId, conversationCode, conversationStepIndex, msg);
            return true;
        }

        private Application app;

        private IDictionary<int, int> conversationWaitingList = new Dictionary<int, int>();

        private const string LOGGER_TAG = "Gate User Session";
    }
}
