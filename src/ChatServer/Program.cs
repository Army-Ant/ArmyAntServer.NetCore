using System;

namespace ArmyAnt.ChatServer
{
    class Program
    {
        private static int Main(params string[] arg)
        {
            return new ChatServerApplication().Start(arg).Result;
        }
    }
}
