using System;
using System.Net;

namespace ArmyAnt.Server.DBProxy {
    static class Program {
        public enum ReturnCode {
            //ServerStartFailed = -5,
            //ModuleInitFailed = -4,
            //ParseConfigJElementFailed = -3,
            ParseConfigJsonFailed = -2,
            //OpenConfigFileFailed = -1,
            NormalExit = 0,
        }

        private static class ServerMainAppid {
            public const int simpleEchoApp = 1001;
            public const int huolongServer = 1010;
        };

#pragma warning disable CS0649
        [System.Serializable]
        private struct Config {
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

        private const string CONFIG_FILE = "../../res/ConfigJson/DBProxyConfig.json";

        private static int ReturnCodeToInt(ReturnCode code) => Convert.ToInt32(code);

        private static int Main(params string[] arg) {
            // Parse config json file
            var jsonFile = System.IO.File.Open(CONFIG_FILE, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Config));
            if(ser.ReadObject(jsonFile) is Config config) {
                jsonFile.Close();
                // Start server
                var proxy = new Application(IO.Logger.LevelFromString(config.logConsoleLevel), IO.Logger.LevelFromString(config.logFileLevel), config.logPath);
                var dbInfo = new MySqlBridge.ConnectOptions {
                    serverAddress = config.mysqlServerHost,
                    serverPort = config.mysqlServerPort,
                    userName = config.mysqlUsername,
                    password = config.mysqlPassword,
                };
                proxy.ConnectDataBase(dbInfo.ToString(), config.mysqlDataBase);
                proxy.Start(new IPEndPoint(IPAddress.Any, config.port));
                // Wait for server ending
                return proxy.AwaitAll().Result;
            }
            jsonFile.Close();
            return ReturnCodeToInt(ReturnCode.ParseConfigJsonFailed);
        }
    }
}
