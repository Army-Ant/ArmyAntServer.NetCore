﻿using System.Collections.Generic;
using ArmyAnt.ServerCore.Event;
using ArmyAnt.ServerCore.SubUnit;
using ArmyAntMessage.System;
using ArmyAntMessage.SubApps;
using ArmyAnt.ServerCore.MsgType;

namespace ArmyAnt.ServerUnits {
    public class SimpleEchoApp : ISubUnit
    {
        public SimpleEchoApp(long appid, ServerCore.Main.Server server) {
            AppId = appid;
            Server = server;
            server.EventManager.RegisterMessage(C2SM_EchoLoginRequest.Descriptor);
            server.EventManager.RegisterMessage(C2SM_EchoLogoutRequest.Descriptor);
            server.EventManager.RegisterMessage(C2SM_EchoSendRequest.Descriptor);
            server.EventManager.RegisterMessage(C2SM_EchoBroadcastRequest.Descriptor);
            server.EventManager.RegisterMessage(SM2C_EchoLoginResponse.Descriptor);
            server.EventManager.RegisterMessage(SM2C_EchoLogoutResponse.Descriptor);
            server.EventManager.RegisterMessage(SM2C_EchoReceiveNotice.Descriptor);
            server.EventManager.RegisterMessage(SM2C_EchoSendResponse.Descriptor);
            server.EventManager.RegisterMessage(SM2C_EchoBroadcastResponse.Descriptor);
            server.EventManager.RegisterMessage(SM2C_EchoError.Descriptor);
        }

        public long AppId { get; }

        public ServerCore.Main.Server Server { get; }

        public long TaskId { get; set; }

        public void OnTask<Input>(int _event, params Input[] data) {
        }

        public void OnNetworkMessage(int code, CustomData info, Google.Protobuf.IMessage data, EndPointTask user) {
            if(data is C2SM_EchoLoginRequest) {
                OnUserLogin(info.conversationCode, user, data as C2SM_EchoLoginRequest);
            } else if(data is C2SM_EchoLogoutRequest) {
                OnUserLogout(info.conversationCode, user, data as C2SM_EchoLogoutRequest);
            } else if(data is C2SM_EchoSendRequest) {
                OnUserSend(info.conversationCode, user, data as C2SM_EchoSendRequest);
            } else if(data is C2SM_EchoBroadcastRequest) {
                OnUserBroadcast(info.conversationCode, user, data as C2SM_EchoBroadcastRequest);
            } else {
                Server.Log(IO.Logger.LogLevel.Warning, LOGGER_TAG, "Received an unknown message, code:", code, ", user:", user.ID);
            }
        }

        public void OnUserSessionDisconnected(long userId) {
            string userName = null;
            lock(loggedUsers) {
                foreach(var i in loggedUsers) {
                    if(i.Value == userId) {
                        userName = i.Key;
                    }
                }
                if(userName != null) { // User name has logged in 
                    loggedUsers.Remove(userName);
                }
            }
            if(userName != null) { // User name has logged in 
                Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "User disconnected: ", userName);
            }
        }

        public void OnUserSessionLogin(long userId) {
        }

        public void OnUserSessionLogout(long userId) {
        }

        public void OnUserSessionReconnected(long userId) {
        }

        public void OnUserSessionShutdown(long userId) {
            OnUserSessionDisconnected(userId);
        }

        public bool Start() {
            return false;
        }

        public bool Stop() {
            return false;
        }

        public System.Threading.Tasks.Task WaitAll() {
            return Server.EventManager.GetTask(TaskId);
        }

        private void OnUserLogin(int conversationCode, EndPointTask user, C2SM_EchoLoginRequest message) {
            var response = new SM2C_EchoLoginResponse();
            lock(loggedUsers) {
                if(loggedUsers.ContainsKey(message.UserName)) { // User name has logged in !
                    response.Result = 3;    // failed
                    response.Message = "The user of this name has logged in";
                    Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "User logged failed with an existed name: ", message.UserName);
                } else {
                    response.Result = 0;
                    response.Message = "Login successful !";
                    loggedUsers.Add(message.UserName, user.ID);
                    Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "User login: ", message.UserName);
                }
            }
            user.SendMessage(new CustomMessageSend<SM2C_EchoLoginResponse> {
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
        }

        private void OnUserLogout(int conversationCode, EndPointTask user, C2SM_EchoLogoutRequest message) {
            var response = new SM2C_EchoLogoutResponse();
            lock(loggedUsers) {
                if(!loggedUsers.ContainsKey(message.UserName)) { // User name has not logged in !
                    response.Result = 3;    // failed
                    response.Message = "The user of this name has not logged in";
                    Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "User logged out failed with an inexisted name: ", message.UserName);
                } else {
                    response.Result = 0;
                    response.Message = "Logout successful !";
                    loggedUsers.Remove(message.UserName);
                    Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "User log out: ", message.UserName);
                }
            }
            user.SendMessage(new CustomMessageSend<SM2C_EchoLogoutResponse> {
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
        }

        private void OnUserSend(int conversationCode, EndPointTask user, C2SM_EchoSendRequest message) {
            var response = new SM2C_EchoSendResponse();
            SM2C_EchoReceiveNotice notice = null;
            string fromUser = null;
            EndPointTask tarUser = null;
            lock(loggedUsers) {
                foreach(var i in loggedUsers) {
                    if(i.Value == user.ID) {
                        fromUser = i.Key;
                    }
                }
                if(fromUser == null) {
                    response.Result = 3;    // failed, sender have not logged in
                    response.Message = "You have not logged in";
                    Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "An unlogged user (index: ", user.ID, ") want to send a message to ", message.Target);
                } else if(!loggedUsers.ContainsKey(message.Target) || !Server.EventManager.IsUserIn(loggedUsers[message.Target])) {
                    response.Result = 4;    // failed, unknown target user
                    response.Message = "The user of this name has not logged in";
                    Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "User ", fromUser, " want to sended a message to an inexist user: ", message.Target);
                } else {
                    var tarUserId = loggedUsers[message.Target];
                    tarUser = Server.EventManager.GetUserSession(tarUserId);
                    response.Result = 0;    // successful
                    response.Message = "Send successful !";
                    Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "User ", fromUser, " sended a message to user: ", message.Target);
                    notice = new SM2C_EchoReceiveNotice() {
                        IsBroadcast = false,
                        From = fromUser,
                        Message = message.Message,
                    };
                }
            }
            response.Request = message;
            user.SendMessage(new CustomMessageSend<SM2C_EchoSendResponse> {
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
            if(notice != null) {
                tarUser.SendMessage(new CustomMessageSend<SM2C_EchoReceiveNotice> {
                    appid = AppId,
                    conversationStepType = ConversationStepType.NoticeOnly,
                    body = notice,
                });
            }
        }

        private void OnUserBroadcast(int conversationCode, EndPointTask user, C2SM_EchoBroadcastRequest message) {
            var response = new SM2C_EchoBroadcastResponse();
            SM2C_EchoReceiveNotice notice = null;
            string fromUser = null;
            lock(loggedUsers) {
                foreach(var i in loggedUsers) {
                    if(i.Value == user.ID) {
                        fromUser = i.Key;
                    }
                }
            }
            if(fromUser == null) {
                response.Result = 3;    // failed, sender have not logged in
                response.Message = "You have not logged in";
                Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "An unlogged user (index: ", user.ID, ") want to send a broadcast message");
            } else {
                response.Result = 0;    // successful
                response.Message = "Send successful !";
                Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "User ", fromUser, " sended a broadcast message");
                notice = new SM2C_EchoReceiveNotice() {
                    IsBroadcast = true,
                    From = fromUser,
                    Message = message.Message,
                };
            }
            response.Request = message;
            user.SendMessage(new CustomMessageSend<SM2C_EchoBroadcastResponse> {
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
            if(notice != null) {
                lock(loggedUsers) {
                    foreach(var i in loggedUsers) {
                        Server.EventManager.GetUserSession(i.Value).SendMessage(new CustomMessageSend<SM2C_EchoReceiveNotice> {
                            appid = AppId,
                            conversationStepType = ConversationStepType.NoticeOnly,
                            body = notice,
                        });
                    }
                }
            }
        }

        private const string LOGGER_TAG = "SimpleEchoApp";

        private IDictionary<string, long> loggedUsers = new Dictionary<string, long>();
    }
}
