using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ArmyAnt.Network;
using ArmyAnt.IO;
using System.Linq;

namespace ArmyAnt.Server.Gate {
    public sealed class Application : IApplication {
        public Event.EventManager EventManager { get; private set; }

        public Application(Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            CommonInitial(consoleLevel, fileLevel, loggerFile);
            dbProxy = new SocketTcpClient() {
                OnClientReceived = OnDBProxyReceived,
                OnTcpClientDisonnected = OnDBProxyDisconnected
            };
        }

        public Application(IPEndPoint dbProxyLocal, Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            CommonInitial(consoleLevel, fileLevel, loggerFile);
            dbProxy = new SocketTcpClient(dbProxyLocal) {
                OnClientReceived = OnDBProxyReceived,
                OnTcpClientDisonnected = OnDBProxyDisconnected
            };
        }

        ~Application() {
            logger.RemoveStream(loggerFile);
            loggerFile.Close();
            try {
                Stop();
            } catch(System.Net.Sockets.SocketException) {
            }
        }

        public void Start(IPEndPoint tcp, IPEndPoint udp, params string[] http) {
            udpListener.Start(udp);
            tcpServer.Start(tcp);
            httpServer.Start(http);
        }

        public void Stop() {
            DisconnectDBProxy();
            httpServer.Stop();
            tcpServer.Stop();
            udpListener.Stop();
            EventManager.ClearUserSession();
            tcpSocketUserList.Clear();
            tcpSocketIndexList.Clear();
            webSocketUserList.Clear();
            webSocketIndexList.Clear();
        }

        public async Task<int> AwaitAll() {
            var (tcpMainTask, allTask) = tcpServer.WaitingTask;
            var udpTask = udpListener.WaitingTask;
            var (httpMainTask, websocketClientsTask) = httpServer.WaitingTask;
            var dbProxyTask = dbProxy.WaitingTask;
            var usersTask = EventManager.GetAllTasks();
            allTask.Add(tcpMainTask);
            allTask.Add(udpTask);
            allTask.Add(httpMainTask);
            allTask.AddRange(websocketClientsTask);
            allTask.Add(dbProxyTask);
            allTask.AddRange(usersTask);
            await Task.WhenAll(allTask);
            return 0;
        }

        public void ConnectDBProxy(string dbProxyAddr, ushort dbProxyPort) {
            this.dbProxy.Connect(IPAddress.Parse(dbProxyAddr), dbProxyPort);
        }

        public void ConnectDBProxy(IPEndPoint dbProxy) {
            this.dbProxy.Connect(dbProxy);
        }

        public void DisconnectDBProxy() {
            dbProxy.Stop();
        }

        public void Log(Logger.LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine(lv, " [ ", Tag, " ] ", content);
        }

        public void Send<T>(NetworkType type, long userId, int conversationStepIndex, CustomMessageSend<T> msg) where T : Google.Protobuf.IMessage {
            int index = 0;
            byte[] sending;
            switch(type) {
                case NetworkType.Tcp:
                    lock(tcpSocketIndexList) {
                        if(!tcpSocketIndexList.ContainsKey(userId)) {
                            Log(Logger.LogLevel.Error, LOGGER_TAG, "Sending TCP message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg.body));
                            return;
                        }
                        index = tcpSocketIndexList[userId];
                    }
                    sending = CustomMessageSend<T>.PackMessage(conversationStepIndex, msg);
                    sendingTasks.Add(tcpServer.Send(index, sending));
                    break;
                case NetworkType.Websocket:
                    lock(webSocketIndexList) {
                        if(!webSocketIndexList.ContainsKey(userId)) {
                            if(!webSocketIndexList.ContainsKey(userId)) {
                                Log(Logger.LogLevel.Error, LOGGER_TAG, "Sending Websocket message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg.body));
                                return;
                            }
                            index = webSocketIndexList[userId];
                        }
                    }
                    sending = CustomMessageSend<T>.PackMessage(conversationStepIndex, msg);
                    sendingTasks.Add(httpServer.Send(index, sending));
                    break;
            }
        }

        private void CommonInitial(Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            EventManager = new Event.EventManager(this);
            logger = new Logger(true, consoleLevel);
            this.loggerFile = Logger.CreateLoggerFileStream(loggerFile);
            logger.AddStream(this.loggerFile, fileLevel, true);
            tcpServer = new SocketTcpServer {
                OnTcpServerConnected = OnTcpServerConnected,
                OnTcpServerDisonnected = OnTcpServerDisonnected,
                OnTcpServerReceived = OnTcpServerReceived
            };
            udpListener = new SocketUdp {
                OnClientReceived = OnUdpReceived
            };
            httpServer = new HttpServer {
                OnHttpServerReceived = OnHttpServerReceived,
                OnTcpServerConnected = OnWebsocketServerConnected,
                OnTcpServerDisonnected = OnWebsocketServerDisonnected,
                OnTcpServerReceived = OnWebsocketServerReceived
            };
        }

        private bool OnTcpServerConnected(int index, IPEndPoint point) {
            lock(tcpSocketUserList) {
                if(tcpSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LOGGER_TAG, "New TCP client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                } else {
                    var user = new User(EventManager, NetworkType.Tcp);
                    Log(Logger.LogLevel.Verbose, LOGGER_TAG, "New TCP client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", user.UserId);
                    lock(tcpSocketIndexList) {
                        tcpSocketUserList.Add(index, user.UserId);
                        tcpSocketIndexList.Add(user.UserId, index);
                    }
                    EventManager.SetSessionOnline(user.UserId);
                    return true;
                }
            }
        }

        private void OnTcpServerDisonnected(int index) {
            lock(tcpSocketUserList) {
                if(tcpSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LOGGER_TAG, "Detected a TCP client disconnected , but no session record here");
                } else {
                    Log(Logger.LogLevel.Verbose, LOGGER_TAG, "A TCP client disconnected");
                }
                lock(tcpSocketIndexList) {
                    long userSession = tcpSocketUserList[index];
                    EventManager.SetSessionLogout(userSession);
                    EventManager.RemoveUserSession(userSession).Wait();
                    tcpSocketUserList.Remove(index);
                    tcpSocketIndexList.Remove(userSession);
                }
            }
        }

        private void OnTcpServerReceived(int index, byte[] data) {
            long userId;
            lock(tcpSocketUserList) {
                if(!tcpSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LOGGER_TAG, "Detected a TCP message , but no session record here!");
                    return;
                }
                userId = tcpSocketUserList[index];
            }
            var msg = CustomMessageReceived.ParseMessage(data);
            OnMessage(userId, msg);
        }

        private void OnUdpReceived(IPEndPoint ep, byte[] data) { /* TODO */ }

        private void OnHttpServerReceived(HttpListenerRequest request, HttpListenerResponse response, System.Security.Principal.IPrincipal user) { /* TODO */ }

        private bool OnWebsocketServerConnected(int index, IPEndPoint point) {
            lock(webSocketUserList) {
                if(webSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LOGGER_TAG, "New Websocket client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                } else {
                    var user = new User(EventManager, NetworkType.Websocket);
                    Log(Logger.LogLevel.Verbose, LOGGER_TAG, "New Websocket client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", user.UserId);
                    lock(webSocketUserList) {
                        webSocketUserList.Add(index, user.UserId);
                        webSocketIndexList.Add(user.UserId, index);
                    }
                    EventManager.SetSessionOnline(user.UserId);
                    return true;
                }
            }
        }

        private void OnWebsocketServerDisonnected(int index) {
            lock(webSocketUserList) {
                if(webSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LOGGER_TAG, "Detected a Websocket client disconnected , but no session record here");
                } else {
                    Log(Logger.LogLevel.Verbose, LOGGER_TAG, "A Websocket client disconnected");
                }
                lock(webSocketUserList) {
                    long userSession = webSocketUserList[index];
                    EventManager.SetSessionLogout(userSession);
                    EventManager.RemoveUserSession(userSession).Wait();
                    webSocketUserList.Remove(index);
                    webSocketIndexList.Remove(userSession);
                }
            }
        }

        private void OnWebsocketServerReceived(int index, byte[] data) {
            long userId;
            lock(webSocketUserList) {
                if(!webSocketUserList.ContainsKey(index)) {
                    Log(Logger.LogLevel.Error, LOGGER_TAG, "Detected a Websocket message , but no session record here!");
                    return;
                }
                userId = webSocketUserList[index];
            }
            var msg = CustomMessageReceived.ParseMessage(data);
            OnMessage(userId, msg);
        }

        private void OnDBProxyReceived(IPEndPoint ep, byte[] data) {  /* TODO */}

        private void OnDBProxyDisconnected() { /* TODO */ }

        private void OnMessage(long userId, CustomMessageReceived msg) {
            Log(Logger.LogLevel.Verbose, LOGGER_TAG, "Received from client id: ", userId, ", appid: " + msg.appid);
            if(!EventManager.IsUserIn(userId)) {
                Log(Logger.LogLevel.Warning, LOGGER_TAG, "Cannot find the user session: ", userId, " when resolving the message");
                return;
            }
            EventManager.DispatchNetworkMessage(msg.messageCode, userId, msg);
        }

        private const string LOGGER_TAG = "Server Main";

        private SocketTcpServer tcpServer;
        private SocketUdp udpListener;
        private HttpServer httpServer;
        private SocketTcpClient dbProxy;
        private System.IO.FileStream loggerFile;
        private Logger logger;

        private IDictionary<int, long> tcpSocketUserList = new Dictionary<int, long>();
        private IDictionary<long, int> tcpSocketIndexList = new Dictionary<long, int>();
        private IDictionary<int, long> webSocketUserList = new Dictionary<int, long>();
        private IDictionary<long, int> webSocketIndexList = new Dictionary<long, int>();
        private IList<Task> sendingTasks = new List<Task>();
    }
}
