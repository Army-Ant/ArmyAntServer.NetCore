using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ArmyAnt.Network;
using ArmyAnt.IO;
using System.Linq;

namespace ArmyAnt.Server.Gate {
    public sealed class Application : IApplication {
        public Event.EventManager EventManager { get; private set; }

        public Application(params string[] loggerFile) {
            CommonInitial(loggerFile);
            dbProxy = new SocketTcpClient() {
                OnClientReceived = OnDBProxyReceived,
                OnTcpClientDisonnected = OnDBProxyDisconnected
            };
        }

        public Application(IPEndPoint dbProxyLocal, params string[] loggerFile) {
            CommonInitial(loggerFile);
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
            webSocketUserList.Clear();
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

        public void Log(LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine("[ ", System.DateTime.Now.ToString(), " ] [ ", lv, " ] [ ", Tag, " ] ", content);
        }

        private void CommonInitial(params string[] loggerFile) {
            EventManager = new Event.EventManager(this);
            logger = new Logger(true);
            this.loggerFile = Logger.CreateLoggerFileStream(loggerFile);
            logger.AddStream(this.loggerFile);
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
                    Log(LogLevel.Error, LOGGER_TAG, "New TCP client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                } else {
                    var user = new User(EventManager, NetworkType.Tcp);
                    Log(LogLevel.Verbose, LOGGER_TAG, "New TCP client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", user.UserId);
                    tcpSocketUserList.Add(index, user.UserId);
                    EventManager.SetSessionOnline(user.UserId);
                    return true;
                }
            }
        }

        private void OnTcpServerDisonnected(int index) {
            lock(tcpSocketUserList) {
                if(tcpSocketUserList.ContainsKey(index)) {
                    Log(LogLevel.Error, LOGGER_TAG, "Detected a TCP client disconnected , but no session record here");
                } else {
                    Log(LogLevel.Verbose, LOGGER_TAG, "A TCP client disconnected");
                }
                long userSession = tcpSocketUserList[index];
                EventManager.SetSessionLogout(userSession);
                EventManager.RemoveUserSession(userSession).Wait();
                tcpSocketUserList.Remove(index);
            }
        }

        private void OnTcpServerReceived(int index, byte[] data) {
            long userId;
            lock(tcpSocketUserList) {
                if(!tcpSocketUserList.ContainsKey(index)) {
                    Log(LogLevel.Error, LOGGER_TAG, "Detected a TCP message , but no session record here!");
                    return;
                }
                userId = tcpSocketUserList[index];
            }
            var msg = ParseMessage(data);
            OnMessage(userId, msg);
        }

        private void OnUdpReceived(IPEndPoint ep, byte[] data) { /* TODO */ }

        private void OnHttpServerReceived(HttpListenerRequest request, HttpListenerResponse response, System.Security.Principal.IPrincipal user) { /* TODO */ }

        private bool OnWebsocketServerConnected(int index, IPEndPoint point) {
            lock(webSocketUserList) {
                if(webSocketUserList.ContainsKey(index)) {
                    Log(LogLevel.Error, LOGGER_TAG, "New Websocket client connected into , but the same IP (", point.Address.ToString(), ") and port (", point.Port, ") connection has found ! Please check the code");
                    return false;
                } else {
                    var user = new User(EventManager, NetworkType.Websocket);
                    Log(LogLevel.Verbose, LOGGER_TAG, "New Websocket client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", user.UserId);
                    webSocketUserList.Add(index, user.UserId);
                    EventManager.SetSessionOnline(user.UserId);
                    return true;
                }
            }
        }

        private void OnWebsocketServerDisonnected(int index) {
            lock(webSocketUserList) {
                if(webSocketUserList.ContainsKey(index)) {
                    Log(LogLevel.Error, LOGGER_TAG, "Detected a Websocket client disconnected , but no session record here");
                } else {
                    Log(LogLevel.Verbose, LOGGER_TAG, "A Websocket client disconnected");
                }
                long userSession = webSocketUserList[index];
                EventManager.SetSessionLogout(userSession);
                EventManager.RemoveUserSession(userSession).Wait();
                webSocketUserList.Remove(index);
            }
        }

        private void OnWebsocketServerReceived(int index, byte[] data) {
            long userId;
            lock(webSocketUserList) {
                if(!webSocketUserList.ContainsKey(index)) {
                    Log(LogLevel.Error, LOGGER_TAG, "Detected a Websocket message , but no session record here!");
                    return;
                }
                userId = webSocketUserList[index];
            }
            var msg = ParseMessage(data);
            OnMessage(userId, msg);
        }

        private void OnDBProxyReceived(IPEndPoint ep, byte[] data) {  /* TODO */}

        private void OnDBProxyDisconnected() { /* TODO */ }

        private CustomMessage ParseMessage(byte[] data) {
            var head = new MessageBaseHead(data);
            switch(head.extendVersion) {
                case 1:
                    var msg = ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1.Parser.ParseFrom(data, 16, head.extendLength);
                    return new CustomMessage {
                        head = head,
                        appid = msg.AppId,
                        contentLength = msg.ContentLength,
                        messageCode = msg.MessageCode,
                        conversationCode = msg.ConversationCode,
                        conversationStepIndex = msg.ConversationStepIndex,
                        conversationStepType = msg.ConversationStepType,
                        body = data.Skip(16+head.extendLength).ToArray(),
                    };
            }
            return default;
        }

        private void OnMessage(long userId, CustomMessage msg) {
            Log(LogLevel.Verbose, LOGGER_TAG, "Received from client id: ", userId, ", appid: " + msg.appid);
            if(!EventManager.IsUserIn(userId)) {
                Log(LogLevel.Warning, LOGGER_TAG, "Cannot find the user session: ", userId, " when resolving the message");
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
        private IDictionary<int, long> webSocketUserList = new Dictionary<int, long>();
    }
}
