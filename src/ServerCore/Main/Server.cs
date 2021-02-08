using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ArmyAnt.Network;
using ArmyAnt.IO;
using ArmyAnt.ServerCore.MsgType;

namespace ArmyAnt.ServerCore.Main {
    public sealed class Server
    {
        public Event.EventManager EventManager { get; private set; }
        public string LoggerTag { get; private set; }

        public Server(ServerOptions options) {
            this.options = options;
            EventManager = new Event.EventManager();
            LoggerTag = options.loggerTag;
            logger = new Logger(true, options.consoleLevel);
            foreach (var i in options.loggerFile)
            {
                var file = Logger.CreateLoggerFileStream(i);
                loggerFile.Add(file);
                logger.AddStream(file, options.fileLevel, true);
            }
            if (options.tcp != null)
            {
                tcpServer = new SocketTcpServer
                {
                    OnTcpServerConnected = OnTcpServerConnected,
                    OnTcpServerDisonnected = OnTcpServerDisonnected,
                    OnTcpServerReceived = OnTcpServerReceived
                };
            }
            if (options.udp != null)
            {
                udpListener = new SocketUdp
                {
                    OnClientReceived = OnUdpReceived
                };
            }
            if (options.http != null)
            {
                httpServer = new HttpServer
                {
                    OnHttpServerReceived = OnHttpServerReceived,
                    OnTcpServerConnected = OnWebsocketServerConnected,
                    OnTcpServerDisonnected = OnWebsocketServerDisonnected,
                    OnTcpServerReceived = OnWebsocketServerReceived
                };
            }
        }

        ~Server() {
            foreach(var i in loggerFile) {
                logger.RemoveStream(i);
                i.Close();
            }
            try {
                Stop();
            } catch(System.Net.Sockets.SocketException) {
            }
        }

        public void Start() {
            udpListener?.Start(options.udp);
            tcpServer?.Start(options.tcp);
            httpServer?.Start(options.http);
            Log(Logger.LogLevel.Info, LoggerTag, "Server started");
        }

        public void Stop() {
            foreach(var i in appList) {
                i.Value.Stop();
                i.Value.WaitAll();
            }
            appList.Clear();
            httpServer.Stop();
            tcpServer.Stop();
            udpListener.Stop();
            EventManager.ClearAllTasks();
            tcpSocketUserList.Clear();
            tcpSocketIndexList.Clear();
            webSocketUserList.Clear();
            webSocketIndexList.Clear();
            Log(Logger.LogLevel.Info, LoggerTag, "Server stopped");
        }

        public long[] StartSubApplication(params SubUnit.ISubUnit[] apps) {
            var ret = new long[apps.Length];
            for(var i = 0; i < apps.Length; ++i) {
                appList.Add(apps[i].AppId, apps[i]);
                EventManager.OnUserSessionLogin += apps[i].OnUserSessionLogin;
                EventManager.OnUserSessionLogout += apps[i].OnUserSessionLogout;
                EventManager.OnUserSessionDisconnected += apps[i].OnUserSessionDisconnected;
                EventManager.OnUserSessionReconnected += apps[i].OnUserSessionReconnected;
                EventManager.OnUserSessionShutdown += apps[i].OnUserSessionShutdown;
                ret[i] = EventManager.AddSubApplicationTask(apps[i]);
                apps[i].Start();
            }
            return ret;
        }

        public void StopSubApplication(params long[] appid) {
            foreach(var i in appid) {
                appList[i].Stop();
                appList[i].WaitAll().Wait();
                appList.Remove(i);
            }
        }

        public SubUnit.ISubUnit GetSubApplication(long appid) {
            try {
                return appList[appid];
            }catch(KeyNotFoundException ) {
                return null;
            }
        }

        public async Task<int> AwaitAll() {
            var allTask = new List<Task>();
            if (tcpServer != null)
            {
                var (tcpMainTask, tcpClientsTask) = tcpServer.WaitingTask;
                allTask.Add(tcpMainTask);
                allTask.AddRange(tcpClientsTask);
            }
            if (udpListener != null)
            {
                var udpTask = udpListener.WaitingTask;
                allTask.Add(udpTask);
            }
            if (httpServer != null)
            {
                var (httpMainTask, websocketClientsTask) = httpServer.WaitingTask;
                allTask.Add(httpMainTask);
                allTask.AddRange(websocketClientsTask);
            }
            var usersTask = EventManager.GetAllTasks();
            allTask.AddRange(usersTask);
            allTask.AddRange(sendingTasks);
            await Task.WhenAll(allTask);
            return 0;
        }

        public void Log(System.Text.Encoding encoding, Logger.LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine(encoding, lv, " [ ", Tag, " ] ", content);
        }

        public void Log(Logger.LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine(lv, " [ ", Tag, " ] ", content);
        }

        public void Send<T>(NetworkType type, long userId, int conversationCode, int conversationStepIndex, CustomMessageSend<T> msg) where T : Google.Protobuf.IMessage<T>, new() {
            int index = 0;
            byte[] sending;
            switch(type) {
                case NetworkType.Tcp:
                lock(tcpSocketIndexList) {
                    if(!tcpSocketIndexList.ContainsKey(userId)) {
                        Log(Logger.LogLevel.Error, LoggerTag, "Sending TCP message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg.body));
                        return;
                    }
                    index = tcpSocketIndexList[userId];
                }
                sending = CustomMessageSend<T>.PackMessage(conversationCode, conversationStepIndex, msg);
                sendingTasks.Add(tcpServer.Send(index, sending));
                break;
                case NetworkType.Websocket:
                lock(webSocketIndexList) {
                    if(!webSocketIndexList.ContainsKey(userId)) {
                        Log(Logger.LogLevel.Error, LoggerTag, "Sending Websocket message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg.body));
                        return;
                    }
                    index = webSocketIndexList[userId];
                }
                sending = CustomMessageSend<T>.PackMessage(conversationCode, conversationStepIndex, msg);
                sendingTasks.Add(httpServer.Send(index, sending));
                break;
            }
        }

        private bool OnTcpServerConnected(int index, IPEndPoint point) {
            lock(tcpSocketUserList) {
                if(tcpSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LoggerTag, "New TCP client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                } else {
                    var user = new Event.EndPointTask(this, NetworkType.Tcp);
                    user.ID = EventManager.AddUserSession(user);
                    Log(Logger.LogLevel.Verbose, LoggerTag, "New TCP client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", user.ID);
                    lock(tcpSocketIndexList) {
                        tcpSocketUserList.Add(index, user);
                        tcpSocketIndexList.Add(user.ID, index);
                    }
                    EventManager.SetSessionOnline(user.ID);
                    return true;
                }
            }
        }

        private void OnTcpServerDisonnected(int index) {
            lock(tcpSocketUserList) {
                if(!tcpSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LoggerTag, "Detected a TCP client disconnected , but no session record here");
                } else {
                    Log(Logger.LogLevel.Verbose, LoggerTag, "A TCP client disconnected");
                }
                lock(tcpSocketIndexList) {
                    long userId = tcpSocketUserList[index].ID;
                    EventManager.SetSessionLogout(userId);
                    EventManager.RemoveUserSession(userId).Wait();
                    tcpSocketUserList.Remove(index);
                    tcpSocketIndexList.Remove(userId);
                }
            }
        }

        private void OnTcpServerReceived(int index, byte[] data) {
            long userId;
            lock(tcpSocketUserList) {
                if(!tcpSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LoggerTag, "Detected a TCP message , but no session record here!");
                    return;
                }
                userId = tcpSocketUserList[index].ID;
            }
            var msg = CustomMessageReceived.ParseMessage(data);
            OnMessage(userId, msg);
        }

        private void OnUdpReceived(IPEndPoint ep, byte[] data) { /* TODO */ }

        private void OnHttpServerReceived(HttpListenerRequest request, HttpListenerResponse response, System.Security.Principal.IPrincipal user) { /* TODO */ }

        private bool OnWebsocketServerConnected(int index, IPEndPoint point) {
            lock(webSocketUserList) {
                if(webSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LoggerTag, "New Websocket client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                } else {
                    var user = new Event.EndPointTask(this, NetworkType.Websocket);
                    user.ID = EventManager.AddUserSession(user);
                    Log(Logger.LogLevel.Verbose, LoggerTag, "New Websocket client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", user.ID);
                    lock(webSocketUserList) {
                        webSocketUserList.Add(index, user);
                        webSocketIndexList.Add(user.ID, index);
                    }
                    EventManager.SetSessionOnline(user.ID);
                    return true;
                }
            }
        }

        private void OnWebsocketServerDisonnected(int index) {
            lock(webSocketUserList) {
                if(!webSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LoggerTag, "Detected a Websocket client disconnected , but no session record here");
                } else {
                    Log(Logger.LogLevel.Verbose, LoggerTag, "A Websocket client disconnected");
                }
                lock(webSocketUserList) {
                    long userId = webSocketUserList[index].ID;
                    EventManager.SetSessionLogout(userId);
                    EventManager.RemoveUserSession(userId).Wait();
                    webSocketUserList.Remove(index);
                    webSocketIndexList.Remove(userId);
                }
            }
        }

        private void OnWebsocketServerReceived(int index, byte[] data) {
            long userId;
            lock(webSocketUserList) {
                if(!webSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LoggerTag, "Detected a Websocket message , but no session record here!");
                    return;
                }
                userId = webSocketUserList[index].ID;
            }
            var msg = CustomMessageReceived.ParseMessage(data);
            OnMessage(userId, msg);
        }


        private void OnMessage(long userId, CustomMessageReceived msg) {
            Log(Logger.LogLevel.Verbose, LoggerTag, "Received from user id: ", userId, ", appid: " + msg.appid);
            if(!EventManager.IsUserIn(userId)) {
                Log(Logger.LogLevel.Warning, LoggerTag, "Cannot find the user session: ", userId, " when resolving the message");
                return;
            }
            EventManager.DispatchNetworkMessage(msg.messageCode, userId, msg);
        }

        private ServerOptions options;

        private SocketTcpServer tcpServer;
        private SocketUdp udpListener;
        private HttpServer httpServer;
        private readonly IList<System.IO.FileStream> loggerFile = new List<System.IO.FileStream>();
        private Logger logger;

        private readonly IDictionary<int, Event.EndPointTask> tcpSocketUserList = new Dictionary<int, Event.EndPointTask>();
        private readonly IDictionary<long, int> tcpSocketIndexList = new Dictionary<long, int>();
        private readonly IDictionary<int, Event.EndPointTask> webSocketUserList = new Dictionary<int, Event.EndPointTask>();
        private readonly IDictionary<long, int> webSocketIndexList = new Dictionary<long, int>();
        private readonly IList<Task> sendingTasks = new List<Task>();
        private readonly IDictionary<long, SubUnit.ISubUnit> appList = new Dictionary<long, SubUnit.ISubUnit>();
    }
}
