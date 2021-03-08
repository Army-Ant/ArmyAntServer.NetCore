using System.Collections.Generic;
using Google.Protobuf;
using ArmyAntMessage.SubApps;
using ArmyAnt.ServerCore.Event;
using ArmyAnt.ServerCore.SubUnit;

namespace ArmyAnt.GateServer
{
    public class GateUnit : ASubUnit
    {
        private struct ConnectedUserData
        {
            public string uid;
            public long userId;
        }

        public GateUnit(long appid, ServerCore.Main.Server server) : base(appid, server, "GateUnit") {
            RegisterMessage(CS_GateLoginRequest.Descriptor, OnLogin);
            RegisterMessage(SC_GateLoginResponse.Descriptor, null);
            RegisterMessage(SC_GateLoginFailureResponse.Descriptor, null);
            RegisterMessage(CS_GateLogoutRequest.Descriptor, OnLogout);
            RegisterMessage(SC_GateLogoutResponse.Descriptor, null);
            RegisterMessage(SC_GateLogoutFailureResponse.Descriptor, null);
            RegisterMessage(CS_GateLogStateRequest.Descriptor, OnLogState);
            RegisterMessage(SC_GateLogStateResponse.Descriptor, null);
            RegisterMessage(SC_GateLogStateFailureResponse.Descriptor, null);
            RegisterMessage(CS_GateClearAuthRequest.Descriptor, OnClearAuth);
            RegisterMessage(SC_GateClearAuthResponse.Descriptor, null);
            RegisterMessage(SC_GateClearAuthFailureResponse.Descriptor, null);
        }

        public override void OnUserSessionDisconnected(long userId) {
            string uid = null;
            lock(uidStateDic) {
                foreach(var i in uidStateDic) {
                    if(i.Value.userId == userId) {
                        uid = i.Key;
                    }
                }
                if(uid != null) { // User name has logged in 
                    uidStateDic.Remove(uid);
                }
            }
            if(uid != null)
            { // User name has logged in 
                Log(IO.Logger.LogLevel.Info, "User disconnected: ", uid);
            }
            else
            {
                Log(IO.Logger.LogLevel.Info, "UnLogged User disconnected: ", userId);
            }
        }

        private void OnLogin(int conversationCode, EndPointTask user, IMessage msg)
        {
            var request = msg as CS_GateLoginRequest;
            switch (request.Type)
            {
                case LoginType.SuperManager:
                    break;
                case LoginType.Guest:
                    break;
                case LoginType.InnerAccount:
                    break;
                case LoginType.ThirdPartyAccount:
                    break;
                default:
                    break;
            }
        }

        private void OnLogout(int conversationCode, EndPointTask user, IMessage msg)
        {
        }

        private void OnLogState(int conversationCode, EndPointTask user, IMessage msg)
        {
        }

        private void OnClearAuth(int conversationCode, EndPointTask user, IMessage msg)
        {
        }

        private IDictionary<string, ConnectedUserData> uidStateDic = new Dictionary<string, ConnectedUserData>();
    }
}
