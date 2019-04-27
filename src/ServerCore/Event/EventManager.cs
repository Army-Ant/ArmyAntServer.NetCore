using System;
using System.Collections.Generic;
using static ArmyAnt.Utilities;

namespace ArmyAnt.Server.Event {
    #region delegates
    public delegate bool OnKeyNotFoundLocalEvent(int _event, LocalEventArg data);
    public delegate void OnKeyNotFoundNetworkMessage(int _event, CustomMessage us);
    public delegate void OnUnknownEvent(int _event, Type arrayType, params object[] data);
    #endregion

    internal enum SpecialEvent : int {
        UserLogin,
        UserLogout,
        UserShutdown,
        UserDisconnected,
        UserReconnected,
    }

    public class EventManager : Thread.TaskPool<int>.ITaskQueue {
        public IApplication ParentApp { get; }
        public EventManager(IApplication parent) {
            ParentApp = parent;
            selfId = taskPool.AddTaskQueue(this);
        }

        public void OnTask<Input>(int _event, params Input[] data) {
            if(data[0] is LocalEventArg local) {
                try {
                    localEventPool[_event]?.Invoke(_event, local);
                } catch(KeyNotFoundException) {
                    OnKeyNotFoundLocalEvent(_event, local);
                }
            } else if(data[0] is CustomMessage net) {
                try {
                    networkEventPool[_event]?.Invoke(_event, net);
                } catch(KeyNotFoundException) {
                    OnKeyNotFoundNetworkMessage(_event, net);
                }
            } else if(data[0] is int user && data.Length == 2 && data[1] is int check && check == 0) {
                switch((SpecialEvent)_event) {
                    case SpecialEvent.UserLogin:
                        OnUserSessionLogin(user);
                        break;
                    case SpecialEvent.UserLogout:
                        OnUserSessionLogout(user);
                        break;
                    case SpecialEvent.UserShutdown:
                        OnUserSessionShutdown(user);
                        break;
                    case SpecialEvent.UserDisconnected:
                        OnUserSessionDisconnected(user);
                        break;
                    case SpecialEvent.UserReconnected:
                        OnUserSessionReconnected(user);
                        break;
                    default:
                        break;
                }
            } else {
                OnUnknownEvent(_event, data.GetType(), data);
            }
        }

        public System.Threading.Tasks.Task[] GetAllTasks() {
            return taskPool.GetAllTasks();
        }
        #region User sessions operation

        public long AddUserSession(IUserSession user) => taskPool.AddTaskQueue(IsNotNull(user));
        public async System.Threading.Tasks.Task<bool> RemoveUserSession(long userId) => await taskPool.RemoveTaskQueue(userId);
        public void ClearUserSession() => taskPool.ClearTaskQueue();
        public bool IsUserIn(long index) => taskPool.IsTaskQueueExist(index);
        public IUserSession GetUserSession(long index) => taskPool.GetQueue(index) as IUserSession;
        public void SetSessionOnline(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserLogin, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserLogin, SpecialEvent.UserLogin);
        }
        public void SetSessionDisconnected(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserDisconnected, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserDisconnected, SpecialEvent.UserDisconnected);
        }
        public void SetSessionReconnected(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserReconnected, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserReconnected, SpecialEvent.UserReconnected);
        }
        public void SetSessionShutdown(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserShutdown, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserShutdown, SpecialEvent.UserShutdown);
        }
        public void SetSessionLogout(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserShutdown, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserShutdown, SpecialEvent.UserShutdown);
        }

        #endregion

        #region Local events

        public void AddLocalEventListener(int code, Action<int, LocalEventArg> _event) {
            IsNotNull(_event);
            lock(localEventPool) {
                try {
                    var tar = localEventPool[code];
                    tar.Add(_event);
                } catch(KeyNotFoundException) {
                    var tar = new EventGroup<LocalEventArg>();
                    tar.Add(_event);
                    localEventPool.Add(code, tar);
                }
            }
        }
        public void RemoveLocalEventListener(int code, Action<int, LocalEventArg> _event) {
            IsNotNull(_event);
            lock(localEventPool) {
                localEventPool[code].Remove(_event);
            }
        }
        public bool DispatchLocalEvent<T>(int code, T data) where T : LocalEventArg => taskPool.EnqueueTaskTo(selfId, code, data);
        public bool DispatchLocalEvent<T>(int code, long userId, T data) where T : LocalEventArg => taskPool.EnqueueTaskTo(userId, code, data);

        #endregion

        #region Network message

        public void AddNetworkMessageListener(int code, Action<int, CustomMessage> _event) {
            IsNotNull(_event);
            lock(networkEventPool) {
                try {
                    var tar = networkEventPool[code];
                    tar.Add(_event);
                } catch(KeyNotFoundException) {
                    var tar = new EventGroup<CustomMessage>();
                    tar.Add(_event);
                    networkEventPool.Add(code, tar);
                }
            }
        }
        public void RemoveNetworkMessageListener(int code, Action<int, CustomMessage> _event) {
            IsNotNull(_event);
            lock(networkEventPool) {
                networkEventPool[code].Remove(_event);
            }
        }
        public bool DispatchNetworkMessage(int code, CustomMessage data) => taskPool.EnqueueTaskTo(selfId, code, data);
        public bool DispatchNetworkMessage(int code, long userId, CustomMessage data) => taskPool.EnqueueTaskTo(userId, code, data);

        #endregion

        public static int GetNetworkMessageCode<T>(T msg) where T : Google.Protobuf.IMessage => msg.Descriptor.CustomOptions.TryGetInt32(50001, out int code) ? code : 0;

        #region Events
        public event OnUserSessionLogin OnUserSessionLogin;
        public event OnUserSessionLogout OnUserSessionLogout;
        public event OnUserSessionShutdown OnUserSessionShutdown;
        public event OnUserSessionDisconnected OnUserSessionDisconnected;
        public event OnUserSessionReconnected OnUserSessionReconnected;
        public event OnKeyNotFoundLocalEvent OnKeyNotFoundLocalEvent;
        public event OnKeyNotFoundNetworkMessage OnKeyNotFoundNetworkMessage;
        public event OnUnknownEvent OnUnknownEvent;
        #endregion

        #region Protected items

        protected class EventGroup<T> {
            public event Action<int, T> OnEvent;
            public void Invoke(int code, T data) => OnEvent(code, data);
            public void Add(Action<int, T> _event) => OnEvent += _event;
            public void Remove(Action<int, T> _event) => OnEvent -= _event;
        }

        #endregion

        private readonly long selfId;
        private readonly Thread.TaskPool<int> taskPool = new Thread.TaskPool<int>();
        private readonly IDictionary<int, EventGroup<LocalEventArg>> localEventPool = new Dictionary<int, EventGroup<LocalEventArg>>();
        private readonly IDictionary<int, EventGroup<CustomMessage>> networkEventPool = new Dictionary<int, EventGroup<CustomMessage>>();
    }
}
