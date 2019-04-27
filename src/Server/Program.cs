using System.Net;

namespace ArmyAnt.Server {
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
            public ushort normalSocketPort;
            public ushort udpSocketPort;
            public ushort websocketPort;
            public string logPath;
            public string logFileLevel;
            public string logConsoleLevel;
            public string dbProxyAddr;
            public ushort dbProxyPort;
        }
#pragma warning restore CS0649

        private const string CONFIG_FILE = "../res/ConfigJson/ServerMainConfig.json";

        private static int ReturnCodeToInt(ReturnCode code) => System.Convert.ToInt32(code);

        private static int Main(params string[] arg) {
            // Parse config json file
            var jsonFile = System.IO.File.Open(CONFIG_FILE, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Config));
            if(ser.ReadObject(jsonFile) is Config config) {
                jsonFile.Close();
                // Start server
                var serverGate = new Gate.Application(config.logPath);
                serverGate.ConnectDBProxy(config.dbProxyAddr, config.dbProxyPort);
                serverGate.Start(new IPEndPoint(IPAddress.Any, config.normalSocketPort), new IPEndPoint(IPAddress.Any, config.udpSocketPort), "http://localhost:" + config.websocketPort + "/", "https://localhost:" + config.websocketPort + "/", "http://127.0.0.1:" + config.websocketPort + "/", "https://127.0.0.1:" + config.websocketPort + "/");
                // Wait for server ending
                return serverGate.AwaitAll().Result;
            }
            jsonFile.Close();
            return ReturnCodeToInt(ReturnCode.ParseConfigJsonFailed);
        }
    }
}
