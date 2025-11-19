using EchoServer.Abstractions;
using System;

namespace EchoServer.Wrappers
{
    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}