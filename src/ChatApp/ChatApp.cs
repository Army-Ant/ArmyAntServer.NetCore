using System.Collections.Generic;
using ArmyAnt.ServerCore.Event;
using ArmyAnt.ServerCore.SubUnit;
using ArmyAntMessage.System;
using ArmyAntMessage.SubApps;

namespace ArmyAnt.ServerUnits {
    public class ChatApp : ISubUnit
    {
        public ChatApp(long appid, ServerCore.Main.Server server) {
            AppId = appid;
            Server = server;
            server.RegisterMessage(CS_EchoLoginRequest.Descriptor);
            server.RegisterMessage(CS_EchoLogoutRequest.Descriptor);
            server.RegisterMessage(CS_EchoSendRequest.Descriptor);
            server.RegisterMessage(CS_EchoBroadcastRequest.Descriptor);
            server.RegisterMessage(SC_EchoLoginResponse.Descriptor);
            server.RegisterMessage(SC_EchoLogoutResponse.Descriptor);
            server.RegisterMessage(SC_EchoReceiveNotice.Descriptor);
            server.RegisterMessage(SC_EchoSendResponse.Descriptor);
            server.RegisterMessage(SC_EchoBroadcastResponse.Descriptor);
            server.RegisterMessage(SC_EchoError.Descriptor);
        }

        public long AppId { get; }

        public ServerCore.Main.Server Server { get; }

        public long TaskId { get; set; }

        public void OnTask<Input>(int _event, params Input[] data) {
        }

        public void OnNetworkMessage(int code, SocketHeadExtend extend, Google.Protobuf.IMessage data, EndPointTask user) {
            if(data is CS_EchoLoginRequest) {
                OnUserLogin(extend.ConversationCode, user, data as CS_EchoLoginRequest);
            } else if(data is CS_EchoLogoutRequest) {
                OnUserLogout(extend.ConversationCode, user, data as CS_EchoLogoutRequest);
            } else if(data is CS_EchoSendRequest) {
                OnUserSend(extend.ConversationCode, user, data as CS_EchoSendRequest);
            } else if(data is CS_EchoBroadcastRequest) {
                OnUserBroadcast(extend.ConversationCode, user, data as CS_EchoBroadcastRequest);
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

        private void OnUserLogin(int conversationCode, EndPointTask user, CS_EchoLoginRequest message) {
            var response = new SC_EchoLoginResponse();
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
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
        }

        private void OnUserLogout(int conversationCode, EndPointTask user, CS_EchoLogoutRequest message) {
            var response = new SC_EchoLogoutResponse();
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
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
        }

        private void OnUserSend(int conversationCode, EndPointTask user, CS_EchoSendRequest message) {
            var response = new SC_EchoSendResponse();
            SC_EchoReceiveNotice notice = null;
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
                    notice = new SC_EchoReceiveNotice() {
                        IsBroadcast = false,
                        From = fromUser,
                        Message = message.Message,
                    };
                }
            }
            response.Request = message;
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
            if (notice != null) {
                tarUser.SendMessage(AppId, ConversationStepType.NoticeOnly, notice, 0);
            }
        }

        private void OnUserBroadcast(int conversationCode, EndPointTask user, CS_EchoBroadcastRequest message) {
            var response = new SC_EchoBroadcastResponse();
            SC_EchoReceiveNotice notice = null;
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
                notice = new SC_EchoReceiveNotice() {
                    IsBroadcast = true,
                    From = fromUser,
                    Message = message.Message,
                };
            }
            response.Request = message;
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
            if (notice != null) {
                lock(loggedUsers) {
                    foreach(var i in loggedUsers) {
                        Server.EventManager.GetUserSession(i.Value).SendMessage(AppId, ConversationStepType.NoticeOnly, notice, 0);
                    }
                }
            }
        }

        private const string LOGGER_TAG = "ChatApp";

        private IDictionary<string, long> loggedUsers = new Dictionary<string, long>();
    }
}
