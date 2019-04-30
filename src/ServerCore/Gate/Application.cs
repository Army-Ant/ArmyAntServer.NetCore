﻿using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ArmyAnt.Network;
using ArmyAnt.IO;
using System.Linq;

namespace ArmyAnt.Server.Gate {
    public sealed class Application {
        public Event.EventManager EventManager { get; private set; }

        public Application(Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            CommonInitial(consoleLevel, fileLevel, loggerFile);
            dbPosLocal = null;
            dbProxy = new SocketTcpClient() {
                OnClientReceived = OnDBProxyReceived,
                OnTcpClientDisonnected = OnDBProxyDisconnected
            };
        }

        public Application(IPEndPoint dbProxyLocal, Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            CommonInitial(consoleLevel, fileLevel, loggerFile);
            dbPosLocal = dbProxyLocal;
            dbProxy = new SocketTcpClient(dbProxyLocal) {
                OnClientReceived = OnDBProxyReceived,
                OnTcpClientDisonnected = OnDBProxyDisconnected
            };
        }

        ~Application() {
            foreach(var i in loggerFile) {
                logger.RemoveStream(i);
                i.Close();
            }
            try {
                Stop();
            } catch(System.Net.Sockets.SocketException) {
            }
        }

        public void Start(IPEndPoint tcp, IPEndPoint udp, params string[] http) {
            udpListener.Start(udp);
            tcpServer.Start(tcp);
            httpServer.Start(http);
            Log(Logger.LogLevel.Info, LOGGER_TAG, "Server started");
        }

        public void Stop() {
            foreach(var i in appList) {
                i.Value.Stop();
                i.Value.WaitAll();
            }
            appList.Clear();
            DisconnectDBProxy();
            httpServer.Stop();
            tcpServer.Stop();
            udpListener.Stop();
            EventManager.ClearAllTasks();
            tcpSocketUserList.Clear();
            tcpSocketIndexList.Clear();
            webSocketUserList.Clear();
            webSocketIndexList.Clear();
            Log(Logger.LogLevel.Info, LOGGER_TAG, "Server stopped");
        }

        public long[] StartSubApplication(params ISubApplication[] apps) {
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

        public ISubApplication GetSubApplication(long appid) {
            try {
                return appList[appid];
            }catch(KeyNotFoundException ) {
                return null;
            }
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
            allTask.AddRange(sendingTasks);
            await Task.WhenAll(allTask);
            return 0;
        }

        public void ConnectDBProxy(string dbProxyAddr, ushort dbProxyPort) {
            try {
                dbProxy.Connect(IPAddress.Parse(dbProxyAddr), dbProxyPort);
                dbPos = dbProxy.ServerIPEndPoint;
            }catch(System.Net.Sockets.SocketException e) {
                Log(System.Text.Encoding.Default, Logger.LogLevel.Warning, LOGGER_TAG, "DBProxy connected failed, message: ", e.Message);
                ConnectDBProxy(dbProxyAddr, dbProxyPort);
            }
            Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy connected");
        }

        public void ConnectDBProxy(IPEndPoint dbProxy) {
            try {
                this.dbProxy.Connect(dbProxy);
                dbPos = this.dbProxy.ServerIPEndPoint;
            } catch(System.Net.Sockets.SocketException e) {
                Log(Logger.LogLevel.Warning, LOGGER_TAG, "DBProxy connected failed, message: ", e.Message);
                ConnectDBProxy(dbProxy);
            }
            Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy connected");
        }

        public void DisconnectDBProxy() {
            dbProxy.Stop();
            Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy over");
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
                        Log(Logger.LogLevel.Error, LOGGER_TAG, "Sending TCP message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg.body));
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
                        Log(Logger.LogLevel.Error, LOGGER_TAG, "Sending Websocket message to an inexist client id: ", userId, ", message code: ", MessageBaseHead.GetNetworkMessageCode(msg.body));
                        return;
                    }
                    index = webSocketIndexList[userId];
                }
                sending = CustomMessageSend<T>.PackMessage(conversationCode, conversationStepIndex, msg);
                sendingTasks.Add(httpServer.Send(index, sending));
                break;
            }
        }

        private void CommonInitial(Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            EventManager = new Event.EventManager(this);
            logger = new Logger(true, consoleLevel);
            foreach(var i in loggerFile) {
                var file = Logger.CreateLoggerFileStream(i);
                this.loggerFile.Add(file);
                logger.AddStream(file, fileLevel, true);
            }
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
                if(!tcpSocketUserList.ContainsKey(index)) {
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
                if(!webSocketUserList.ContainsKey(index)) {
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

        private void OnDBProxyDisconnected() {
            Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy server lost!");
            if(dbPosLocal == null) {
                dbProxy = new SocketTcpClient() {
                    OnClientReceived = OnDBProxyReceived,
                    OnTcpClientDisonnected = OnDBProxyDisconnected
                };
            } else {
                dbProxy = new SocketTcpClient(dbPosLocal) {
                    OnClientReceived = OnDBProxyReceived,
                    OnTcpClientDisonnected = OnDBProxyDisconnected
                };
            }
            ConnectDBProxy(dbPos);
        }

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
        private IPEndPoint dbPos;
        private IPEndPoint dbPosLocal;
        private IList<System.IO.FileStream> loggerFile = new List<System.IO.FileStream>();
        private Logger logger;

        private readonly IDictionary<int, long> tcpSocketUserList = new Dictionary<int, long>();
        private readonly IDictionary<long, int> tcpSocketIndexList = new Dictionary<long, int>();
        private readonly IDictionary<int, long> webSocketUserList = new Dictionary<int, long>();
        private readonly IDictionary<long, int> webSocketIndexList = new Dictionary<long, int>();
        private readonly IList<Task> sendingTasks = new List<Task>();
        private readonly IDictionary<long, ISubApplication> appList = new Dictionary<long, ISubApplication>();
    }
}
