using System;
using Google.Protobuf;

namespace ArmyAnt.Server.Event {
    #region delegates
    public delegate void OnUserSessionLogin(long index);
    public delegate void OnUserSessionLogout(long index);
    public delegate void OnUserSessionShutdown(long index);
    public delegate void OnUserSessionDisconnected(long index);
    public delegate void OnUserSessionReconnected(long index);
    #endregion

    public class LocalEventArg : EventArgs {
        public int code;
    }

    public enum SessionStatus {
        UnInitialized,
        Online,     // Logged in or reconnected
        Busy,
        Disconnected,   // Disconnected and waiting for reconnection
        Shutdowned, // Disconnected and no longer waiting for reconnection
        Offline,    // Logged out
    }

    public interface IUserSession : Thread.TaskPool<int>.ITaskQueue {
        EventManager EventManager { get;}
        long UserId { get; }
        SessionStatus SessionStatus { get; }
        void OnLocalEvent<T>(int code, T data) where T: LocalEventArg;
        void OnNetworkMessage(int code, CustomMessageReceived data) ;
    }

    public abstract class AUserSession : IUserSession {
        public AUserSession(EventManager parent) {
            EventManager = parent;
            UserId = parent.AddUserSession(this);
        }

        ~AUserSession() {
            EventManager?.RemoveUserSession(UserId);
        }

        #region Realized interface properties

        public EventManager EventManager { get; private set; }
        public long UserId { get; private set; }
        public SessionStatus SessionStatus { get; private set; }

        #endregion

        #region Abstract interface functions

        public abstract void OnLocalEvent<T>(int code, T data) where T : LocalEventArg;
        public abstract void OnNetworkMessage(int code, CustomMessageReceived data);
        public abstract void OnUnknownEvent<T>(int _event, params T[] data);

        public void OnTask<Input>(int _event, params Input[] data) {
            if(data[0] is LocalEventArg local) {
                OnLocalEvent(_event, local);
            } else if(data[0] is CustomMessageReceived net) {
                OnNetworkMessage(_event, net);
            } else if(data[0] is int user && data.Length == 2 && data[1] is int check && check == 0) {
                switch((SpecialEvent)_event) {
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
            } else {
                OnUnknownEvent(_event, data);
            }
        }

        #endregion

        #region Own realized abstract functions
        public abstract void OnUserSessionLogin(long index);
        public abstract void OnUserSessionLogout(long index);
        public abstract void OnUserSessionShutdown(long index);
        public abstract void OnUserSessionDisconnected(long index);
        public abstract void OnUserSessionReconnected(long index);
        #endregion
    }

}
