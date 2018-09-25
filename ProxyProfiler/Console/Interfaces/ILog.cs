using System;

namespace Console.Interfaces
{
    public interface ILog
    {
        void Debug(string message);

        void Debug(string message, Exception ex);
    }
}
