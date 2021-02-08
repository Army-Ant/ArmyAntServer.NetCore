using System.Collections.Generic;
using ArmyAnt.ServerCore.Event;
using ArmyAnt.ServerCore.SubUnit;
using ArmyAntMessage.System;
using ArmyAntMessage.SubApps;
using ArmyAnt.ServerCore.MsgType;

namespace ArmyAnt.ServerUnits {
    public class SimpleEchoApp : ISubUnit
    {
        static SimpleEchoApp() {
            codeDic.Add(typeof(C2SM_EchoLoginRequest), MessageBaseHead.GetNetworkMessageCode(C2SM_EchoLoginRequest.Descriptor));
            codeDic.Add(typeof(C2SM_EchoLogoutRequest), MessageBaseHead.GetNetworkMessageCode(C2SM_EchoLogoutRequest.Descriptor));
            codeDic.Add(typeof(C2SM_EchoSendRequest), MessageBaseHead.GetNetworkMessageCode(C2SM_EchoSendRequest.Descriptor));
            codeDic.Add(typeof(C2SM_EchoBroadcastRequest), MessageBaseHead.GetNetworkMessageCode(C2SM_EchoBroadcastRequest.Descriptor));
            codeDic.Add(typeof(SM2C_EchoLoginResponse), MessageBaseHead.GetNetworkMessageCode(SM2C_EchoLoginResponse.Descriptor));
            codeDic.Add(typeof(SM2C_EchoLogoutResponse), MessageBaseHead.GetNetworkMessageCode(SM2C_EchoLogoutResponse.Descriptor));
            codeDic.Add(typeof(SM2C_EchoReceiveNotice), MessageBaseHead.GetNetworkMessageCode(SM2C_EchoReceiveNotice.Descriptor));
            codeDic.Add(typeof(SM2C_EchoSendResponse), MessageBaseHead.GetNetworkMessageCode(SM2C_EchoSendResponse.Descriptor));
            codeDic.Add(typeof(SM2C_EchoBroadcastResponse), MessageBaseHead.GetNetworkMessageCode(SM2C_EchoBroadcastResponse.Descriptor));
            codeDic.Add(typeof(SM2C_EchoError), MessageBaseHead.GetNetworkMessageCode(SM2C_EchoError.Descriptor));
        }

        public SimpleEchoApp(long appid, ServerCore.Main.Server server) {
            AppId = appid;
            Server = server;
        }

        public long AppId { get; }

        public ServerCore.Main.Server Server { get; }

        public long TaskId { get; set; }

        public void OnTask<Input>(int _event, params Input[] data) {
        }

        public void OnNetworkMessage(int code, CustomMessageReceived data, EndPointTask user) {
            if(code == codeDic[typeof(C2SM_EchoLoginRequest)]) {
                OnUserLogin(data.head, data.conversationCode, user, C2SM_EchoLoginRequest.Parser.ParseFrom(data.body));
            } else if(code == codeDic[typeof(C2SM_EchoLogoutRequest)]) {
                OnUserLogout(data.head, data.conversationCode, user, C2SM_EchoLogoutRequest.Parser.ParseFrom(data.body));
            } else if(code == codeDic[typeof(C2SM_EchoSendRequest)]) {
                OnUserSend(data.head, data.conversationCode, user, C2SM_EchoSendRequest.Parser.ParseFrom(data.body));
            } else if(code == codeDic[typeof(C2SM_EchoBroadcastRequest)]) {
                OnUserBroadcast(data.head, data.conversationCode, user, C2SM_EchoBroadcastRequest.Parser.ParseFrom(data.body));
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
                if(userName != null) { // EndPointTask name has logged in 
                    loggedUsers.Remove(userName);
                }
            }
            if(userName != null) { // EndPointTask name has logged in 
                Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "EndPointTask disconnected: ", userName);
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

        private void OnUserLogin(MessageBaseHead head, int conversationCode, EndPointTask user, C2SM_EchoLoginRequest message) {
            var response = new SM2C_EchoLoginResponse();
            lock(loggedUsers) {
                if(loggedUsers.ContainsKey(message.UserName)) { // EndPointTask name has logged in !
                    response.Result = 3;    // failed
                    response.Message = "The user of this name has logged in";
                    Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "EndPointTask logged failed with an existed name: ", message.UserName);
                } else {
                    response.Result = 0;
                    response.Message = "Login successful !";
                    loggedUsers.Add(message.UserName, user.ID);
                    Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "EndPointTask login: ", message.UserName);
                }
            }
            user.SendMessage(new CustomMessageSend<SM2C_EchoLoginResponse> {
                head = head,
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
        }

        private void OnUserLogout(MessageBaseHead head, int conversationCode, EndPointTask user, C2SM_EchoLogoutRequest message) {
            var response = new SM2C_EchoLogoutResponse();
            lock(loggedUsers) {
                if(!loggedUsers.ContainsKey(message.UserName)) { // EndPointTask name has not logged in !
                    response.Result = 3;    // failed
                    response.Message = "The user of this name has not logged in";
                    Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "EndPointTask logged out failed with an inexisted name: ", message.UserName);
                } else {
                    response.Result = 0;
                    response.Message = "Logout successful !";
                    loggedUsers.Remove(message.UserName);
                    Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "EndPointTask log out: ", message.UserName);
                }
            }
            user.SendMessage(new CustomMessageSend<SM2C_EchoLogoutResponse> {
                head = head,
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
        }

        private void OnUserSend(MessageBaseHead head, int conversationCode, EndPointTask user, C2SM_EchoSendRequest message) {
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
                    Server.Log(IO.Logger.LogLevel.Info, LOGGER_TAG, "EndPointTask ", fromUser, " want to sended a message to an inexist user: ", message.Target);
                } else {
                    var tarUserId = loggedUsers[message.Target];
                    tarUser = Server.EventManager.GetUserSession(tarUserId) as EndPointTask;
                    response.Result = 0;    // successful
                    response.Message = "Send successful !";
                    Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "EndPointTask ", fromUser, " sended a message to user: ", message.Target);
                    notice = new SM2C_EchoReceiveNotice() {
                        IsBroadcast = false,
                        From = fromUser,
                        Message = message.Message,
                    };
                }
            }
            response.Request = message;
            user.SendMessage(new CustomMessageSend<SM2C_EchoSendResponse> {
                head = head,
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
            if(notice != null) {
                tarUser.SendMessage(new CustomMessageSend<SM2C_EchoReceiveNotice> {
                    head = head,
                    appid = AppId,
                    conversationStepType = ConversationStepType.NoticeOnly,
                    body = notice,
                });
            }
        }

        private void OnUserBroadcast(MessageBaseHead head, int conversationCode, EndPointTask user, C2SM_EchoBroadcastRequest message) {
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
                Server.Log(IO.Logger.LogLevel.Verbose, LOGGER_TAG, "EndPointTask ", fromUser, " sended a broadcast message");
                notice = new SM2C_EchoReceiveNotice() {
                    IsBroadcast = true,
                    From = fromUser,
                    Message = message.Message,
                };
            }
            response.Request = message;
            user.SendMessage(new CustomMessageSend<SM2C_EchoBroadcastResponse> {
                head = head,
                appid = AppId,
                conversationStepType = ConversationStepType.ResponseEnd,
                body = response,
            }, conversationCode);
            if(notice != null) {
                lock(loggedUsers) {
                    foreach(var i in loggedUsers) {
                        (Server.EventManager.GetUserSession(i.Value) as EndPointTask).SendMessage(new CustomMessageSend<SM2C_EchoReceiveNotice> {
                            head = head,
                            appid = AppId,
                            conversationStepType = ConversationStepType.NoticeOnly,
                            body = notice,
                        });
                    }
                }
            }
        }

        private const string LOGGER_TAG = "SimpleEchoApp";

        private static readonly IDictionary<System.Type, int> codeDic = new Dictionary<System.Type, int>();

        private IDictionary<string, long> loggedUsers = new Dictionary<string, long>();
    }
}
