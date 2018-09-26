using System;

namespace ProxyProfiler.Interfaces
{
    public interface IMethodExecutionHistory
    {
        object[] Args { get; }

        long ElapsedMilliseconds { get; }

        DateTime ExecutionDateTime { get; }
    }
}
