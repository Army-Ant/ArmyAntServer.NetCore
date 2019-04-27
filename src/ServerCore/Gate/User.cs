using System;
using System.Collections.Generic;
using System.Text;

namespace ArmyAnt.Server.Gate {
    public class User : Event.AUserSession {
        public User(Event.EventManager mgr,      Network.NetworkType clientType) : base(mgr) {
            this.clientType = clientType;
        }

        public override void OnLocalEvent<T>(int code, T data) {
        }

        public override void OnNetworkMessage(int code, CustomMessage data) {
            bool contain;
            bool stepEqual;
            lock(conversationWaitingList) {
                contain = conversationWaitingList.ContainsKey(data.conversationCode);
                stepEqual = conversationWaitingList[data.conversationCode] == data.conversationStepIndex;
            }
            // Checking message step data
            switch(data.conversationStepType) {
                case ArmyAntMessage.System.ConversationStepType.NoticeOnly:
                    if(contain) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Notice should not has the same code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for notice, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    }
                    break;
                case ArmyAntMessage.System.ConversationStepType.AskFor:
                    if(contain) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Ask-for should not has the same code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for ask-for, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Record the ask-for
                        lock(conversationWaitingList) {
                            conversationWaitingList.Add(data.conversationCode, 0);
                        }
                    }
                    break;
                case ArmyAntMessage.System.ConversationStepType.StartConversation:
                    if(contain) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Conversation-start should not has the same code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: " , data.conversationCode);
                        return;
                    } else if(data.conversationStepIndex != 0) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-start, user: ", UserId, " , message code: " , code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Record the conversation
                        lock(conversationWaitingList) {
                            conversationWaitingList.Add(data.conversationCode, 1);
                        }
                    }
                    break;
                case ArmyAntMessage.System.ConversationStepType.ConversationStepOn:
                    if(!contain) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Conversation-step should has the past data code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(!stepEqual) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for conversation-step, user: ", UserId, " , message code: " , code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Record the conversation step
                        lock(conversationWaitingList) {
                            conversationWaitingList[data.conversationCode] = 1+ conversationWaitingList[data.conversationCode];
                        }
                    }
                    break;
                case ArmyAntMessage.System.ConversationStepType.ResponseEnd:
                    if(!contain) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "The end should has the past data code in waiting list, user: ", UserId, " , message code: ", code, " , conversation code: ", data.conversationCode);
                        return;
                    } else if(stepEqual && data.conversationStepIndex != 0) {
                        EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Wrong waiting step index for end, user: ", UserId, " , message code: ", code, " , conversation step: ", data.conversationStepIndex);
                        return;
                    } else {
                        // Remove the waiting data
                        lock(conversationWaitingList) {
                            conversationWaitingList.Remove(data.conversationCode);
                        }
                    }
                    break;
                default:
                    EventManager.ParentApp.Log(LogLevel.Warning, LOGGER_TAG, "Network message unknown type number, user: ", UserId, " , message code: ", code, " , conversation type: " , (long)data.conversationStepType);
                    return;
            }
            // TODO: resolve message

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

        private void SendMessage(byte[] msg) {
            // 这是从 C++ 的 ArmyAntServer 复制过来的发送代码, 因为时间来不及, 先放在这里提交, 回去处理
            auto evt = static_cast<NetworkSendStruct*>(msg.pdata);
            int32 conversationStepIndex = 0;
            bool ret = true;
            ioMutex.lock()
                ;
            bool isEnd = false;
            int32 first = 0;
            int32 second = 0;
            {
                // 因为发生了死锁问题, 所以决定将数据取出来立即解锁, 以解决拖锁导致死锁的问题
                auto lastConversation = conversationWaitingList.find(evt->conversationCode);
                isEnd = lastConversation == conversationWaitingList.end();
                if(!isEnd) {
                    first = lastConversation->first;
                    second = lastConversation->second;
                }
            }
            ioMutex.unlock();
            switch(evt->conversationStepType) {
                case ArmyAntMessage::System::ConversationStepType::NoticeOnly:
                case ArmyAntMessage::System::ConversationStepType::AskFor:
                case ArmyAntMessage::System::ConversationStepType::StartConversation:
                    conversationStepIndex = 0;
                    if(!isEnd) {
                        mgr.logger.pushLog(ArmyAnt::String("Sending a network message as conversation start with an existed code: ") + int64(evt->conversationCode), ArmyAnt::Logger::AlertLevel::Error, LOGGER_TAG);
                        ret = false;
                    }
                    break;
                case ArmyAntMessage::System::ConversationStepType::ConversationStepOn:
                    if(!isEnd && second == 0) {
                        mgr.logger.pushLog(ArmyAnt::String("Sending a network message as asking reply with an existed normal conversation code: ") + int64(evt->conversationCode), ArmyAnt::Logger::AlertLevel::Error, LOGGER_TAG);
                        ret = false;
                    }
                case ArmyAntMessage::System::ConversationStepType::ResponseEnd:
                    if(isEnd) {
                        mgr.logger.pushLog(ArmyAnt::String("Sending a network message as conversation reply with an unexisted code: ") + int64(evt->conversationCode), ArmyAnt::Logger::AlertLevel::Error, LOGGER_TAG);
                        ret = false;
                    } else {
                        conversationStepIndex = second;
                        if(conversationStepIndex == 0)
                            conversationStepIndex = 1;
                    }
                    break;
                default:
                    mgr.logger.pushLog(ArmyAnt::String("Unknown conversation step type when sending a network message: ") + int64(evt->conversationStepType), ArmyAnt::Logger::AlertLevel::Error, LOGGER_TAG);
                    ret = false;
            }
            if(ret) {
                switch(evt->extendVersion) {
                    case 1: {
                        ArmyAntMessage::System::SocketExtendNormal_V0_0_0_1 extend;
                        extend.set_app_id(evt->appid);
                        extend.set_content_length(evt->length);
                        extend.set_message_code(evt->code);
                        extend.set_conversation_code(evt->conversationCode);
                        extend.set_conversation_step_index(conversationStepIndex);
                        extend.set_conversation_step_type(evt->conversationStepType);
                        socketSender.send(senderIndex, 0, MessageType::Normal, evt->extendVersion, extend, evt->data);
                        break;
                    }
                    default:
                        mgr.logger.pushLog(ArmyAnt::String("Sending a network message with an unknown head version: ") + int64(evt->extendVersion), ArmyAnt::Logger::AlertLevel::Error, LOGGER_TAG);
                        ret = false;
                }

                ioMutex.lock()
                    ;
                switch(evt->conversationStepType) {
                    case ArmyAntMessage::System::ConversationStepType::AskFor:
                        conversationWaitingList.insert(std::make_pair(evt->conversationCode, 0));
                        break;
                    case ArmyAntMessage::System::ConversationStepType::StartConversation:
                        conversationWaitingList.insert(std::make_pair(evt->conversationCode, 1));
                        break;
                    case ArmyAntMessage::System::ConversationStepType::ConversationStepOn: {
                        auto inserting = std::make_pair(first, second + 1);
                        conversationWaitingList.erase(first);
                        conversationWaitingList.insert(inserting);
                        break;
                    }
                    case ArmyAntMessage::System::ConversationStepType::ResponseEnd:
                        conversationWaitingList.erase(first);
                        break;
                    default:
                        // TODO: Warning
                        break;
                }
                ioMutex.unlock();
            }
        }

    private Network.NetworkType clientType;
    private IDictionary<int, int> conversationWaitingList = new Dictionary<int, int>();

        private const string LOGGER_TAG = "Gate User Session";
    }
}
