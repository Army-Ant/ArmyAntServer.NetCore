using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ArmyAnt.IO;
using ArmyAnt.Network;

namespace ArmyAnt.Server.DBProxy {
    public class Application {

        public Application(Logger.LogLevel consoleLevel, Logger.LogLevel fileLevel, params string[] loggerFile) {
            logger = new Logger(true, consoleLevel);
            this.loggerFile = Logger.CreateLoggerFileStream(loggerFile);
            logger.AddStream(this.loggerFile, fileLevel, true);
            tcpServer = new SocketTcpServer {
                OnTcpServerConnected = OnTcpServerConnected,
                OnTcpServerDisonnected = OnTcpServerDisonnected,
                OnTcpServerReceived = OnTcpServerReceived
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

        public void Start(IPEndPoint tcp) {
            tcpServer.Start(tcp);
        }

        public void Stop() {
            tcpServer.Stop();
        }

        public async Task<int> AwaitAll() {
            var (tcpMainTask, allTask) = tcpServer.WaitingTask;
            allTask.Add(tcpMainTask);
            await Task.WhenAll(allTask);
            return 0;
        }

        public void Log(Logger.LogLevel lv, string Tag, params object[] content) {
            logger.WriteLine(lv, "[ ", System.DateTime.Now.ToString(), " ] [ ", lv, " ] [ ", Tag, " ] ", content);
        }

        public void Send<T>(NetworkType type, int index, int conversationStepIndex, CustomMessageSend<T> msg) where T : Google.Protobuf.IMessage {
            var sending = CustomMessageSend < T >.PackMessage(conversationStepIndex, msg);
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
            // TODO: resolve
        }

        private const string LOGGER_TAG = "Server Main";

        private SocketTcpServer tcpServer;
        private System.IO.FileStream loggerFile;
        private Logger logger;
        private IList<Task> sendingTasks = new List<Task>();
    }
}
