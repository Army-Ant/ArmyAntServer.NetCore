using System.Collections.Generic;
using ArmyAnt.ServerCore.Event;
using ArmyAnt.ServerCore.SubUnit;
using ArmyAntMessage.System;
using ArmyAntMessage.SubApps;
using Google.Protobuf;

namespace ArmyAnt.ChatServer
{
    public class ChatUnit : ASubUnit
    {
        public ChatUnit(long appid, ServerCore.Main.Server server) : base(appid, server, "ChatUnit")
        {
            RegisterMessage(CS_EchoLoginRequest.Descriptor, OnUserLogin);
            RegisterMessage(CS_EchoLogoutRequest.Descriptor, OnUserLogout);
            RegisterMessage(CS_EchoSendRequest.Descriptor, OnUserSend);
            RegisterMessage(CS_EchoBroadcastRequest.Descriptor, OnUserBroadcast);
            RegisterMessage(SC_EchoLoginResponse.Descriptor, null);
            RegisterMessage(SC_EchoLogoutResponse.Descriptor, null);
            RegisterMessage(SC_EchoReceiveNotice.Descriptor, null);
            RegisterMessage(SC_EchoSendResponse.Descriptor, null);
            RegisterMessage(SC_EchoBroadcastResponse.Descriptor, null);
            RegisterMessage(SC_EchoError.Descriptor, null);
        }

        public override void OnUserSessionDisconnected(long userId) {
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
                Log(IO.Logger.LogLevel.Info, "User disconnected: ", userName);
            }
        }

        private void OnUserLogin(int conversationCode, EndPointTask user, IMessage msg) {
            var request = msg as CS_EchoLoginRequest;
            var response = new SC_EchoLoginResponse();
            lock(loggedUsers) {
                if(loggedUsers.ContainsKey(request.UserName)) { // User name has logged in !
                    response.Result = 3;    // failed
                    response.Message = "The user of this name has logged in";
                    Log(IO.Logger.LogLevel.Info, "User logged failed with an existed name: ", request.UserName);
                } else {
                    response.Result = 0;
                    response.Message = "Login successful !";
                    loggedUsers.Add(request.UserName, user.ID);
                    Log(IO.Logger.LogLevel.Verbose, "User login: ", request.UserName);
                }
            }
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
        }

        private void OnUserLogout(int conversationCode, EndPointTask user, IMessage msg)
        {
            var request = msg as CS_EchoLogoutRequest;
            var response = new SC_EchoLogoutResponse();
            lock(loggedUsers) {
                if(!loggedUsers.ContainsKey(request.UserName)) { // User name has not logged in !
                    response.Result = 3;    // failed
                    response.Message = "The user of this name has not logged in";
                    Log(IO.Logger.LogLevel.Info, "User logged out failed with an inexisted name: ", request.UserName);
                } else {
                    response.Result = 0;
                    response.Message = "Logout successful !";
                    loggedUsers.Remove(request.UserName);
                    Log(IO.Logger.LogLevel.Verbose, "User log out: ", request.UserName);
                }
            }
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
        }

        private void OnUserSend(int conversationCode, EndPointTask user, IMessage msg)
        {
            var request = msg as CS_EchoSendRequest;
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
                    Log(IO.Logger.LogLevel.Info, "An unlogged user (index: ", user.ID, ") want to send a message to ", request.Target);
                } else if(!loggedUsers.ContainsKey(request.Target) || !Server.EventManager.IsUserIn(loggedUsers[request.Target])) {
                    response.Result = 4;    // failed, unknown target user
                    response.Message = "The user of this name has not logged in";
                    Log(IO.Logger.LogLevel.Info, "User ", fromUser, " want to sended a message to an inexist user: ", request.Target);
                } else {
                    var tarUserId = loggedUsers[request.Target];
                    tarUser = Server.EventManager.GetUserSession(tarUserId);
                    response.Result = 0;    // successful
                    response.Message = "Send successful !";
                    Log(IO.Logger.LogLevel.Verbose, "User ", fromUser, " sended a message to user: ", request.Target);
                    notice = new SC_EchoReceiveNotice() {
                        IsBroadcast = false,
                        From = fromUser,
                        Message = request.Message,
                    };
                }
            }
            response.Request = request;
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
            if (notice != null) {
                tarUser.SendMessage(AppId, ConversationStepType.NoticeOnly, notice, 0);
            }
        }

        private void OnUserBroadcast(int conversationCode, EndPointTask user, IMessage msg)
        {
            var request = msg as CS_EchoBroadcastRequest;
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
                Log(IO.Logger.LogLevel.Info, "An unlogged user (index: ", user.ID, ") want to send a broadcast message");
            } else {
                response.Result = 0;    // successful
                response.Message = "Send successful !";
                Log(IO.Logger.LogLevel.Verbose, "User ", fromUser, " sended a broadcast message");
                notice = new SC_EchoReceiveNotice() {
                    IsBroadcast = true,
                    From = fromUser,
                    Message = request.Message,
                };
            }
            response.Request = request;
            user.SendMessage(AppId, ConversationStepType.ResponseEnd, response, conversationCode);
            if (notice != null) {
                lock(loggedUsers) {
                    foreach(var i in loggedUsers) {
                        Server.EventManager.GetUserSession(i.Value).SendMessage(AppId, ConversationStepType.NoticeOnly, notice, 0);
                    }
                }
            }
        }

        private IDictionary<string, long> loggedUsers = new Dictionary<string, long>();
    }
}
