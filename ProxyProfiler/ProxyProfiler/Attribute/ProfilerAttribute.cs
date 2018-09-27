using ProxyProfiler.Interfaces;
using System;
using System.Reflection;

namespace ProxyProfiler.Attribute
{
    /// <summary>
    /// Represents the base class for all profiler attributes. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public abstract class ProfilerAttribute : System.Attribute, IProfilerAttribute
    {
        /// <summary>
        /// Gets the type of the profiler that will be intantiated for each method
        /// </summary>
        public Type ProfilerType { get; private set; }

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        /// <param name="type">Type of the profiler that will be intantiated for each method</param>
        protected ProfilerAttribute(Type type)
        {
            ProfilerType = type;
        }

        /// <summary>
        /// Method invoked before the invocation of profiled method
        /// </summary>
        /// <typeparam name="T">Type of the profiler that was provided at the constructor</typeparam>
        /// <param name="profiler">Instance of the profiler that will be injected with provided type</param>
        /// <param name="methodToInvoke">Method info of profiled method</param>
        /// <param name="beforeInvokeArgs">Arguments that will be passed to profiled method</param>
        public virtual void OnBeforeInvoke<T>(T profiler, MethodInfo methodToInvoke, object[] beforeInvokeArgs) where T : class, new() { }

        /// <summary>
        /// Method invoked after the invocation of profiled method
        /// </summary>
        /// <typeparam name="T">Type of the profiler that was provided at the constructor</typeparam>
        /// <param name="profiler">Instance of the profiler that will be injected with provided type</param>
        /// <param name="invokedMethod">Method info of profiled method</param>
        /// <param name="beforeInvokeArgs">Arguments that were passed to profiled method</param>
        /// <param name="afterInvokeArgs">Arguments that were passed to profiled method after the invocation. (For 'out' and 'ref' arguments)</param>
        /// <param name="invokeResult">Object returned from profiled method invocation</param>
        public virtual void OnAfterInvoke<T>(T profiler, MethodInfo invokedMethod, object[] beforeInvokeArgs, object[] afterInvokeArgs, object invokeResult) where T : class, new() { }

        /// <summary>
        /// Method invoked if there was an exception at invocation of profiled method
        /// </summary>
        /// <typeparam name="T">Type of the profiler that was provided at the constructor</typeparam>
        /// <param name="profiler">Instance of the profiler that will be injected with provided type</param>
        /// <param name="invokedMethod">Method info of profiled method</param>
        /// <param name="exception">Exception thrown</param>
        public virtual void OnInvokeException<T>(T profiler, MethodInfo invokedMethod, Exception exception) where T : class, new() { }
    }
}
