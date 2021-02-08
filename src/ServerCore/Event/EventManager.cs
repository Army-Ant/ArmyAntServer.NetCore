using ArmyAnt.ServerCore.MsgType;

using System;
using System.Collections.Generic;
using static ArmyAnt.Utilities;

namespace ArmyAnt.ServerCore.Event {
    #region delegates
    public delegate bool OnKeyNotFoundLocalEvent(int _event, LocalEventArg data);
    public delegate void OnKeyNotFoundNetworkMessage(int _event, CustomMessageReceived us);
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

        public void RegisterMessage(Google.Protobuf.Reflection.MessageDescriptor descriptor)
        {
            var code = descriptor.GetOptions().GetExtension(BaseExtensions.MsgCode);
            messageTypeDic[code] = descriptor;
        }

        public void OnTask<Input>(int _event, params Input[] data) {
            if (data[0] is LocalEventArg local) {
                try {
                    localEventPool[_event]?.Invoke(_event, local);
                } catch (KeyNotFoundException) {
                    OnKeyNotFoundLocalEvent?.Invoke(_event, local);
                }
            } else if (data[0] is CustomMessageReceived net) {
                try {
                    networkEventPool[_event]?.Invoke(_event, net);
                } catch (KeyNotFoundException) {
                    OnKeyNotFoundNetworkMessage?.Invoke(_event, net);
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

        public void AddNetworkMessageListener(int code, Action<int, CustomMessageReceived> _event) {
            IsNotNull(_event);
            lock (networkEventPool) {
                try {
                    var tar = networkEventPool[code];
                    tar.Add(_event);
                } catch (KeyNotFoundException) {
                    var tar = new EventGroup<CustomMessageReceived>();
                    tar.Add(_event);
                    networkEventPool.Add(code, tar);
                }
            }
        }
        public void RemoveNetworkMessageListener(int code, Action<int, CustomMessageReceived> _event) {
            IsNotNull(_event);
            lock (networkEventPool) {
                networkEventPool[code].Remove(_event);
            }
        }

        public bool DispatchNetworkMessage(int code, CustomMessageReceived data) => taskPool.EnqueueTaskTo(selfId, code, data);

        public bool DispatchNetworkMessage(int code, long userId, CustomData data, string json)
        {
            if (!messageTypeDic.ContainsKey(code))
            {
                return false;
            }
            else
            {
                return taskPool.EnqueueTaskTo<object>(userId, code, data, messageTypeDic[code].Parser.ParseJson(json));
            }
        }

        public bool DispatchNetworkMessage(int code, long userId, CustomMessageReceived data)
        {
            if (!messageTypeDic.ContainsKey(code))
            {
                return false;
            }
            else
            {
                return taskPool.EnqueueTaskTo<object>(userId, code, data, messageTypeDic[code].Parser.ParseFrom(data.body), data.head);
            }
        }

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
        private readonly IDictionary<int, EventGroup<CustomMessageReceived>> networkEventPool = new Dictionary<int, EventGroup<CustomMessageReceived>>();
        private readonly IDictionary<int, Google.Protobuf.Reflection.MessageDescriptor> messageTypeDic = new Dictionary<int, Google.Protobuf.Reflection.MessageDescriptor>();
    }
}
