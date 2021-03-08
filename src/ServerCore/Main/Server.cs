using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ArmyAnt.Network;
using ArmyAnt.IO;
using ArmyAnt.MsgType;
using ArmyAntMessage.System;
using System.Text;
using Google.Protobuf;

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
            msgHelper = new MessageHelper();
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

        public bool StartSubApplication(SubUnit.ISubUnit app)
        {
            appList.Add(app.AppId, app);
            EventManager.OnUserSessionLogin += app.OnUserSessionLogin;
            EventManager.OnUserSessionLogout += app.OnUserSessionLogout;
            EventManager.OnUserSessionDisconnected += app.OnUserSessionDisconnected;
            EventManager.OnUserSessionReconnected += app.OnUserSessionReconnected;
            EventManager.OnUserSessionShutdown += app.OnUserSessionShutdown;
            var ret = app.Start();
            if (ret)
            {
                app.TaskId = EventManager.AddSubApplicationTask(app);
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

        public void RegisterMessage(Google.Protobuf.Reflection.MessageDescriptor descriptor) => msgHelper.RegisterMessage(descriptor);

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

        public void Log(Encoding encoding, Logger.LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine(encoding, lv, " [ ", Tag, " ] ", content);
        }

        public void Log(Logger.LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine(lv, " [ ", Tag, " ] ", content);
        }

        public void Send<T>(MessageType msgType, NetworkType type, long userId, SocketHeadExtend extend, T msg) where T : IMessage<T>, new() {
            int index = 0;
            byte[] sending;
            if (msgType == MessageType.JsonString)
            {
                sending = Encoding.Default.GetBytes(msgHelper.SerializeJson(extend, msg));
            }
            else
            {
                sending = msgHelper.SerializeBinary(extend, msg);
            }
            switch (type)
            {
                case NetworkType.Tcp:
                    lock (tcpSocketIndexList)
                    {
                        if (!tcpSocketIndexList.ContainsKey(userId))
                        {
                            Log(Logger.LogLevel.Error, LoggerTag, "Sending TCP message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg));
                            return;
                        }
                        index = tcpSocketIndexList[userId];
                    }
                    sendingTasks.Add(tcpServer.Send(index, sending));
                    break;
                case NetworkType.WebSocket:
                    lock (webSocketIndexList)
                    {
                        if (!webSocketIndexList.ContainsKey(userId))
                        {
                            Log(Logger.LogLevel.Error, LoggerTag, "Sending Websocket message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg));
                            return;
                        }
                        index = webSocketIndexList[userId];
                    }
                    sendingTasks.Add(httpServer.Send(index, sending));
                    break;
                default:
                    Log(Logger.LogLevel.Error, LoggerTag, "Wrong NetworkType value to send message: ", type);
                    break;
            }
        }

        public void SendUDP<T>(MessageType msgType, IPEndPoint target, SocketHeadExtend extend, T msg) where T : IMessage<T>, new()
        {
            byte[] sending;
            if (msgType == MessageType.JsonString)
            {
                sending = Encoding.Default.GetBytes(msgHelper.SerializeJson(extend, msg));
            }
            else
            {
                sending = msgHelper.SerializeBinary(extend, msg);
            }
            sendingTasks.Add(udpListener.Send(target, sending));
        }

        private bool OnTcpServerConnected(int index, IPEndPoint point)
        {
            lock (tcpSocketUserList)
            {
                if (tcpSocketUserList.ContainsKey(index))
                {
                    Log(Logger.LogLevel.Error, LoggerTag, "New TCP client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                }
                else
                {
                    var user = new Event.EndPointTask(this, NetworkType.Tcp);
                    user.ID = EventManager.AddUserSession(user);
                    Log(Logger.LogLevel.Verbose, LoggerTag, "New TCP client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", user.ID);
                    lock (tcpSocketIndexList)
                    {
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
            var (extend, msg, msgType) = msgHelper.Deserialize(options.tcpAllowJson, data);
            OnMessage(userId, extend, msg, msgType);
        }

        private void OnUdpReceived(IPEndPoint ep, byte[] data) {
            var (extend, msg, msgType) = msgHelper.Deserialize(options.udpAllowJson, data);
            Log(Logger.LogLevel.Verbose, LoggerTag, "Received UDP message from ip: ", ep.Address.ToString(), ", port: " + ep.Port);
            EventManager.DispatchUDPMessage(MessageBaseHead.GetNetworkMessageCode(msg), extend, msg);

        }

        private void OnHttpServerReceived(HttpListenerRequest request, HttpListenerResponse response, System.Security.Principal.IPrincipal user) {
            /* TODO */ 
        }

        private bool OnWebsocketServerConnected(int index, IPEndPoint point) {
            lock(webSocketUserList) {
                if(webSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LoggerTag, "New Websocket client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                } else {
                    var user = new Event.EndPointTask(this, NetworkType.WebSocket);
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
            var (extend, msg, msgType) = msgHelper.Deserialize(options.tcpAllowJson, data);
            OnMessage(userId, extend, msg, msgType);
        }

        private void OnMessage(long userId, SocketHeadExtend extend, IMessage msg, MessageType msgType)
        {
            Log(Logger.LogLevel.Verbose, LoggerTag, "Received from user id: ", userId, ", appid: " + extend.AppId);
            if (!EventManager.IsUserIn(userId))
            {
                Log(Logger.LogLevel.Warning, LoggerTag, "Cannot find the user session: ", userId, " when resolving the message");
                return;
            }
            EventManager.GetUserSession(userId).msgType = msgType;
            EventManager.DispatchNetworkMessage(MessageBaseHead.GetNetworkMessageCode(msg), userId, extend, msg);
        }

        private ServerOptions options;

        private SocketTcpServer tcpServer;
        private SocketUdp udpListener;
        private HttpServer httpServer;
        private MessageHelper msgHelper;
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
