using ArmyAnt.IO;
using System;
using System.Net;
using System.Threading.Tasks;

namespace ArmyAnt.DBProxyServer
{
    internal class DBProxyServerApplication
    {
        private enum ReturnCode
        {
            //ServerStartFailed = -5,
            //ModuleInitFailed = -4,
            //ParseConfigJElementFailed = -3,
            ParseConfigJsonFailed = -2,
            //OpenConfigFileFailed = -1,
            NormalExit = 0,
        }

        private static class ServerMainAppid
        {
            public const int simpleEchoApp = 1001;
            public const int huolongServer = 1010;
        };

#pragma warning disable CS0649
        [System.Serializable]
        private struct Config
        {
            public bool debug;
            public ushort port;
            public string logPath;
            public string logFileLevel;
            public string logConsoleLevel; // TODO: 未实现
            public string mysqlServerHost;
            public string mysqlServerPort;
            public string mysqlDataBase;
            public string mysqlUsername;
            public string mysqlPassword;
        }
#pragma warning restore CS0649

        ~DBProxyServerApplication()
        {
            try
            {
                DisconnectDataBase();
            }
            catch (System.Net.Sockets.SocketException)
            {
            }
        }

        public async Task<int> Start(params string[] arg)
        {
            // Parse config json file
            var jsonFile = System.IO.File.Open(CONFIG_FILE, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Config));
            if (ser.ReadObject(jsonFile) is Config config)
            {
                jsonFile.Close();

                // define server
                server = new ServerCore.Main.Server(new ServerCore.Main.ServerOptions
                {
                    consoleLevel = Logger.LevelFromString(config.logConsoleLevel),
                    fileLevel = Logger.LevelFromString(config.logFileLevel),
                    loggerFile = new string[] { config.logPath },
                    loggerTag = LOGGER_TAG,
                    tcp = new IPEndPoint(IPAddress.Any, config.port),
                });

                // Connect Data Base
                var dbInfo = new MySqlBridge.ConnectOptions
                {
                    serverAddress = config.mysqlServerHost,
                    serverPort = config.mysqlServerPort,
                    userName = config.mysqlUsername,
                    password = config.mysqlPassword,
                };
                ConnectDataBase(dbInfo.ToString(), config.mysqlDataBase);

                // Start server
                server.Start();

                // Wait for server ending
                var ret = await server.AwaitAll();
                DisconnectDataBase();
                return ret;
            }
            jsonFile.Close();
            return ReturnCodeToInt(ReturnCode.ParseConfigJsonFailed);
        }


        private void ConnectDataBase(string connStr, string defaultDataBase)
        {
            mySql = new MySqlBridge();
            while (!mySql.Connect(connStr, defaultDataBase))
            {
                server.Log(Logger.LogLevel.Error, LOGGER_TAG, "Database connected failed, retrying...");
            }
            server.Log(Logger.LogLevel.Info, LOGGER_TAG, "Database connected");
        }

        private void DisconnectDataBase()
        {
            mySql.Disconnect();
            server.Log(Logger.LogLevel.Info, LOGGER_TAG, "Database disconnected");
        }

        private const string LOGGER_TAG = "DBProxy Main";
        private const string CONFIG_FILE = "../../res/ConfigJson/DBProxyConfig.json";

        private int ReturnCodeToInt(ReturnCode code) => Convert.ToInt32(code);

        private ServerCore.Main.Server server;
        private MySqlBridge mySql;
    }
}
