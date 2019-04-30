namespace ArmyAnt.Server {
    public interface ISubApplication : Thread.TaskPool<int>.ITaskQueue {
        long AppId { get; }
        Gate.Application Server { get; }
        long TaskId { get; set; }
        bool Start();
        bool Stop();
        System.Threading.Tasks.Task WaitAll();
        void OnUserSessionLogin(long userId);
        void OnUserSessionLogout(long userId);
        void OnUserSessionShutdown(long userId);
        void OnUserSessionDisconnected(long userId);
        void OnUserSessionReconnected(long userId);
        void OnNetworkMessage(int code, CustomMessageReceived data, Gate.User user);
    }
}
