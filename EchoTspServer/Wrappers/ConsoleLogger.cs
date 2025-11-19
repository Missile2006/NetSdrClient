using System;
using System.Diagnostics.CodeAnalysis;
using EchoServer.Abstractions;

namespace EchoServer.Wrappers
{
    [ExcludeFromCodeCoverage]
    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}