using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ArmyAnt.IO;
using ArmyAnt.Network;

namespace ArmyAnt.Server.DBProxy {
    public class Application {

        public Application(Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            mySql = new MySqlBridge();
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
        }

        ~Application() {
            foreach(var i in loggerFile) {
                logger.RemoveStream(i);
                i.Close();
            }
            try {
                DisconnectDataBase();
                Stop();
            } catch(System.Net.Sockets.SocketException) {
            }
        }

        public void Start(IPEndPoint tcp) {
            tcpServer.Start(tcp);
            Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy started");
        }

        public void Stop() {
            tcpServer.Stop();
            Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy stopped");
        }

        public async Task<int> AwaitAll() {
            var (tcpMainTask, allTask) = tcpServer.WaitingTask;
            if(tcpMainTask != null) {
                allTask.Add(tcpMainTask);
            }
            if(allTask.Count > 0) {
                await Task.WhenAll(allTask);
            }
            return 0;
        }

        public void ConnectDataBase(string connStr, string defaultDataBase) {
            while(! mySql.Connect(connStr, defaultDataBase)) {
                Log(Logger.LogLevel.Error, LOGGER_TAG, "Database connected failed, retrying...");
            }
            Log(Logger.LogLevel.Info, LOGGER_TAG, "Database connected");
        }

        public void DisconnectDataBase() {
            mySql.Disconnect();
            Log(Logger.LogLevel.Info, LOGGER_TAG, "Database disconnected");
        }

        public void Log(Logger.LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine(lv, "[ ", Tag, " ] ", content);
        }

        public void Send<T>(NetworkType type, int index, int conversationCode, int conversationStepIndex, CustomMessageSend<T> msg) where T : Google.Protobuf.IMessage<T>, new() {
            var sending = CustomMessageSend <T>.PackMessage(conversationCode, conversationStepIndex, msg);
            sendingTasks.Add(tcpServer.Send(index, sending));
        }

        private bool OnTcpServerConnected(int index, IPEndPoint point) {
            Log(Logger.LogLevel.Verbose, LOGGER_TAG, "New TCP client connected , IP: ", point.Address.ToString(), ", port: ", point.Port, ", has record to client index: ", index);

            return true;
        }

        private void OnTcpServerDisonnected(int index) {
            Log(Logger.LogLevel.Verbose, LOGGER_TAG, "A TCP client disconnected, index:", index);
        }

        private void OnTcpServerReceived(int index, byte[] data) {
            var msg = CustomMessageReceived.ParseMessage(data);
            Log(Logger.LogLevel.Verbose, LOGGER_TAG, "Received from client index: " , index, ", appid: ", msg.appid);
            // TODO: resolve
        }

        private const string LOGGER_TAG = "DBProxy Main";

        private SocketTcpServer tcpServer;
        private MySqlBridge mySql;
        private IList<System.IO.FileStream> loggerFile = new List<System.IO.FileStream>();
        private Logger logger;
        private IList<Task> sendingTasks = new List<Task>();
    }
}
