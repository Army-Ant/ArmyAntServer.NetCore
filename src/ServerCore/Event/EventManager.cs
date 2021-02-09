using ArmyAntMessage.System;

using Google.Protobuf;

using System;
using System.Collections.Generic;
using static ArmyAnt.Utilities;

namespace ArmyAnt.ServerCore.Event {
    #region delegates
    public delegate bool OnKeyNotFoundLocalEvent(int _event, LocalEventArg data);
    public delegate void OnKeyNotFoundNetworkMessage(int _event, SocketHeadExtend extend, IMessage msg);
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
        public EventManager() {
            selfId = taskPool.AddTaskQueue(this);
        }

        public void OnTask<Input>(int _event, params Input[] data) {
            if (data[0] is LocalEventArg local) {
                try {
                    localEventPool[_event]?.Invoke(_event, local);
                } catch (KeyNotFoundException) {
                    OnKeyNotFoundLocalEvent?.Invoke(_event, local);
                }
            } else if (data[0] is SocketHeadExtend extend && data.Length == 2 && data[1] is IMessage msg) {
                try {
                    networkEventPool[_event]?.Invoke(_event, extend, msg);
                } catch (KeyNotFoundException) {
                    OnKeyNotFoundNetworkMessage?.Invoke(_event, extend, msg);
                }
            } else if (data[0] is long user && data.Length == 2 && data[1] is long check && check == 0) {
                switch ((SpecialEvent)_event) {
                    case SpecialEvent.UserLogin:
                        OnUserSessionLogin?.Invoke(user);
                        break;
                    case SpecialEvent.UserLogout:
                        OnUserSessionLogout?.Invoke(user);
                        break;
                    case SpecialEvent.UserShutdown:
                        OnUserSessionShutdown?.Invoke(user);
                        break;
                    case SpecialEvent.UserDisconnected:
                        OnUserSessionDisconnected?.Invoke(user);
                        break;
                    case SpecialEvent.UserReconnected:
                        OnUserSessionReconnected?.Invoke(user);
                        break;
                    default:
                        break;
                }
            } else {
                OnUnknownEvent?.Invoke(_event, data.GetType(), data);
            }
        }

        public System.Threading.Tasks.Task GetTask(long id) => taskPool.GetTask(id);

        public System.Threading.Tasks.Task[] GetAllTasks() => taskPool.GetAllTasks();

        public void ClearAllTasks() => taskPool.ClearTaskQueue();

        #region User sessions operation

        public long AddUserSession(EndPointTask user) => taskPool.AddTaskQueue(IsNotNull(user));
        public async System.Threading.Tasks.Task<bool> RemoveUserSession(long userId) => await taskPool.RemoveTaskQueue(userId);
        public bool IsUserIn(long index) => taskPool.IsTaskQueueExist(index);
        public EndPointTask GetUserSession(long index) => taskPool.GetQueue(index) as EndPointTask;
        public void SetSessionOnline(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserLogin, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserLogin, index);
        }
        public void SetSessionDisconnected(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserDisconnected, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserDisconnected, index);
        }
        public void SetSessionReconnected(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserReconnected, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserReconnected, index);
        }
        public void SetSessionShutdown(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserShutdown, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserShutdown, index);
        }
        public void SetSessionLogout(long index) {
            taskPool.EnqueueTaskTo(selfId, (int)SpecialEvent.UserShutdown, index, 0);
            taskPool.EnqueueTaskTo(index, (int)SpecialEvent.UserShutdown, index);
        }

        #endregion

        #region SubApplications

        public long AddSubApplicationTask(SubUnit.ISubUnit app) => taskPool.AddTaskQueue(IsNotNull(app));
        public async System.Threading.Tasks.Task<bool> RemoveSubApplicationTask(long appTaskId) => await taskPool.RemoveTaskQueue(appTaskId);
        public bool IsSubApplicationIn(long taskId) => taskPool.IsTaskQueueExist(taskId);
        public SubUnit.ISubUnit GetSubApplication(long taskId) => taskPool.GetQueue(taskId) as SubUnit.ISubUnit;

        #endregion

        #region Local events

        public void AddLocalEventListener(int code, Action<int, LocalEventArg> _event) {
            IsNotNull(_event);
            lock (localEventPool) {
                try {
                    var tar = localEventPool[code];
                    tar.Add(_event);
                } catch (KeyNotFoundException) {
                    var tar = new EventGroup<LocalEventArg>();
                    tar.Add(_event);
                    localEventPool.Add(code, tar);
                }
            }
        }
        public void RemoveLocalEventListener(int code, Action<int, LocalEventArg> _event) {
            IsNotNull(_event);
            lock (localEventPool) {
                localEventPool[code].Remove(_event);
            }
        }
        public bool DispatchLocalEvent<T>(int code, T data) where T : LocalEventArg => taskPool.EnqueueTaskTo(selfId, code, data);
        public bool DispatchLocalEvent<T>(int code, long userId, T data) where T : LocalEventArg => taskPool.EnqueueTaskTo(userId, code, data);

        #endregion

        #region Network message

        public void AddNetworkMessageListener(int code, Action<int, SocketHeadExtend, IMessage> _event) {
            IsNotNull(_event);
            lock (networkEventPool) {
                try {
                    var tar = networkEventPool[code];
                    tar.Add(_event);
                } catch (KeyNotFoundException) {
                    var tar = new EventGroup<SocketHeadExtend, IMessage>();
                    tar.Add(_event);
                    networkEventPool.Add(code, tar);
                }
            }
        }
        public void RemoveNetworkMessageListener(int code, Action<int, SocketHeadExtend, IMessage> _event) {
            IsNotNull(_event);
            lock (networkEventPool) {
                networkEventPool[code].Remove(_event);
            }
        }

        public bool DispatchNetworkMessage(int code, SocketHeadExtend extend, IMessage msg) => taskPool.EnqueueTaskTo(selfId, code, extend, msg);

        public bool DispatchNetworkMessage(int code, long userId, SocketHeadExtend extend, IMessage msg) => taskPool.EnqueueTaskTo(userId, code, extend, msg);
        

        #endregion

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

        protected class EventGroup<T>
        {
            public event Action<int, T> OnEvent;
            public void Invoke(int code, T data) => OnEvent(code, data);
            public void Add(Action<int, T> _event) => OnEvent += _event;
            public void Remove(Action<int, T> _event) => OnEvent -= _event;
        }

        protected class EventGroup<T1, T2>
        {
            public event Action<int, T1, T2> OnEvent;
            public void Invoke(int code, T1 data1, T2 data2) => OnEvent(code, data1, data2);
            public void Add(Action<int, T1, T2> _event) => OnEvent += _event;
            public void Remove(Action<int, T1, T2> _event) => OnEvent -= _event;
        }

        #endregion

        private readonly long selfId;
        private readonly Thread.TaskPool<int> taskPool = new Thread.TaskPool<int>();
        private readonly IDictionary<int, EventGroup<LocalEventArg>> localEventPool = new Dictionary<int, EventGroup<LocalEventArg>>();
        private readonly IDictionary<int, EventGroup<SocketHeadExtend, IMessage>> networkEventPool = new Dictionary<int, EventGroup<SocketHeadExtend, IMessage>>();
    }
}
