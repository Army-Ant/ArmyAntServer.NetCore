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
        public System.Net.IPEndPoint udp;
        public string[] http;
    }
}
