using System;
using System.Net;

namespace ArmyAnt.GateServer
{
    static class Program
    {

        private static int Main(params string[] arg)
        {
            return new GateServerApplication().Start(arg).Result;
        }
    }
}
