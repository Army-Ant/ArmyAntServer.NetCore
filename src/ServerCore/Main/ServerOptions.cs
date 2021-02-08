using System;
using System.Collections.Generic;
using System.Text;

using ArmyAnt.IO;

namespace ArmyAnt.ServerCore.Main
{
    public struct ServerOptions
    {
        public Logger.LogLevel consoleLevel;
        public Logger.LogLevel fileLevel;
        public string[] loggerFile;
        public string loggerTag;
        public System.Net.IPEndPoint tcp;
        public bool tcpAllowJson;
        public System.Net.IPEndPoint udp;
        public bool udpAllowJson;
        public string[] http;
        public bool websocketAllowJson;
    }
}
