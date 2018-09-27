using Console.Interfaces;
using ProxyProfiler.Attribute;
using System;
using System.Reflection;

namespace Console.Attributes
{
    public class LogAttribute : ProfilerAttribute
    {
        public LogAttribute(Type type) : base(type)
        {

        }

        public override void OnBeforeInvoke<T>(T profiler, MethodInfo methodToInvoke, object[] beforeInvokeArgs)
        {
            var instance = (ILog)profiler;

            instance.Debug("teste atributo before");
        }

        public override void OnAfterInvoke<T>(T profiler, MethodInfo invokedMethod, object[] beforeInvokeArgs, object[] afterInvokeArgs, object invokeResult)
        {
            var instance = (ILog)profiler;

            instance.Debug("teste atributo after " + invokeResult?.ToString());
        }

        public override void OnInvokeException<T>(T profiler, MethodInfo invokedMethod, Exception exception)
        {
            var instance = (ILog)profiler;

            instance.Debug("teste atributo exception", exception);
        }
    }
}
