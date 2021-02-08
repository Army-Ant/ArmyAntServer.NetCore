using System;
using System.Net;

namespace ArmyAnt.DBProxyServer {
    static class Program {
        public static int Main(params string[] arg) {
            return new DBProxyServerApplication().Start(arg).Result;
        }
    }
}
