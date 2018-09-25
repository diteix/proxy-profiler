using Console.Interfaces;
using System;

namespace Console.Classes
{
    public class Log : ILog
    {
        public void Debug(string message)
        {
            System.Console.WriteLine(message);
        }

        public void Debug(string message, Exception ex)
        {
            System.Console.WriteLine("Message: {0}, StackTrace: {1}", message, ex.StackTrace);
        }
    }
}
