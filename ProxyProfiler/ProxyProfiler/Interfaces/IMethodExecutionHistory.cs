using System;

namespace ProxyProfiler.Interfaces
{
    public interface IMethodExecutionHistory
    {
        object[] BeforeInvokeArgs { get; }

        object[] AfterInvokeArgs { get; }

        long ElapsedMilliseconds { get; }

        DateTime ExecutionDateTime { get; }
    }
}
