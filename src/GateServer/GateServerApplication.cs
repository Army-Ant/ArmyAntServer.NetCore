using ArmyAnt.IO;
using ArmyAnt.Network;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace ArmyAnt.GateServer
{
    internal class GateServerApplication
    {
        public enum ReturnCode
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
            public const long simpleEchoApp = 1001;
            public const long huolongServer = 1010;
        };

#pragma warning disable CS0649
        [System.Serializable]
        private struct Config
        {
            public bool debug;
            public ushort normalSocketPort;
            public ushort SSLSocketPort;
            public ushort udpSocketPort;
            public ushort websocketPort;
            public ushort websocketSSLPort;
            public string logPath;
            public string logFileLevel;
            public string logConsoleLevel;
            public string dbProxyAddr;
            public ushort dbProxyPort;
        }
#pragma warning restore CS0649

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
                    tcp = new IPEndPoint(IPAddress.Any, config.normalSocketPort),
                    udp = new IPEndPoint(IPAddress.Any, config.udpSocketPort),
                    http = new string[] { "http://localhost:" + config.websocketPort + "/", "https://localhost:" + config.websocketSSLPort + "/", "http://127.0.0.1:" + config.websocketPort + "/", "https://127.0.0.1:" + config.websocketSSLPort + "/" },
                });

                // Connect DB proxy server
                dbProxy = new SocketTcpClient()
                {
                    OnClientReceived = OnDBProxyReceived,
                    OnTcpClientDisonnected = OnDBProxyDisconnected
                };
                while (!ConnectDBProxy(config.dbProxyAddr, config.dbProxyPort)) ;

                // Start server
                server.Start();

                var simpleEchoApp = new ServerUnits.SimpleEchoApp(ServerMainAppid.simpleEchoApp, server);
                simpleEchoApp.TaskId = server.StartSubApplication(simpleEchoApp)[0];

                // Wait for server ending
                var ret = await server.AwaitAll();
                dbProxy.Close();
                await dbProxy.WaitingTask;
                return ret;
            }
            jsonFile.Close();
            return ReturnCodeToInt(ReturnCode.ParseConfigJsonFailed);
        }


        private void OnDBProxyReceived(IPEndPoint ep, byte[] data) {  /* TODO */}

        private void OnDBProxyDisconnected()
        {
            server.Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy server lost!");
            dbProxy = new SocketTcpClient()
            {
                OnClientReceived = OnDBProxyReceived,
                OnTcpClientDisonnected = OnDBProxyDisconnected
            };
            while (!ConnectDBProxy(dbPos)) ;
        }

        private bool ConnectDBProxy(string dbProxyAddr, ushort dbProxyPort)
        {
            try
            {
                dbProxy.Connect(IPAddress.Parse(dbProxyAddr), dbProxyPort);
                dbPos = dbProxy.ServerIPEndPoint;
            }
            catch (System.Net.Sockets.SocketException e)
            {
                server.Log(System.Text.Encoding.Default, Logger.LogLevel.Warning, LOGGER_TAG, "DBProxy connected failed, message: ", e.Message);
                return false;
            }
            server.Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy connected");
            return true;
        }

        private bool ConnectDBProxy(IPEndPoint dbProxy)
        {
            try
            {
                this.dbProxy.Connect(dbProxy);
                dbPos = this.dbProxy.ServerIPEndPoint;
            }
            catch (System.Net.Sockets.SocketException e)
            {
                server.Log(Logger.LogLevel.Warning, LOGGER_TAG, "DBProxy connected failed, message: ", e.Message);
                return false;
            }
            server.Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy connected");
            return true;
        }

        private void DisconnectDBProxy()
        {
            dbProxy.Stop();
            server.Log(Logger.LogLevel.Info, LOGGER_TAG, "DBProxy over");
        }

        private int ReturnCodeToInt(ReturnCode code) => Convert.ToInt32(code);

        private IPEndPoint dbPos;

        private ServerCore.Main.Server server;
        private SocketTcpClient dbProxy;

        private const string LOGGER_TAG = "Gate Server";
        private const string CONFIG_FILE = "../../res/ConfigJson/ServerMainConfig.json";
    }
}
