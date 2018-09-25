using ProxyProfiler.Interfaces;
using System;
using System.Reflection;

namespace ProxyProfiler.Attribute
{
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class ProfilerAttribute : System.Attribute, IProfilerAttribute
    {
        public Type ProfileType { get; private set; }

        protected ProfilerAttribute(Type type)
        {
            ProfileType = type;
        }

        public virtual void OnBeforeInvoke<T>(T profiler, MethodInfo methodToInvoke) where T : class, new() { }

        public virtual void OnAfterInvoke<T>(T profiler, MethodInfo invokedMethod, object invokeResult) where T : class, new() { }

        public virtual void OnInvokeException<T>(T profiler, MethodInfo invokedMethod, Exception exception) where T : class, new() { }
    }
}
