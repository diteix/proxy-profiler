using ProxyProfiler.Classes;
using ProxyProfiler.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;

namespace ProxyProfiler
{
    public class ProxyProfiler<T> : RealProxy where T : class
    {
        private readonly T _profiledObject;

        private static IDictionary<int, MethodProfilerInfo<T>> _cache = new HybridDictionary<int, MethodProfilerInfo<T>>(true);

        private ProxyProfiler(T profiledObject) : base(typeof(T))
        {
            _profiledObject = profiledObject;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = msg as IMethodCallMessage;
            var methodInfo = methodCall.MethodBase as MethodInfo;

            MethodProfilerInfo<T> methodProfileInfo;

            if ((methodProfileInfo = GetMethodProfileInfo(methodInfo)) == null)
            {
                methodProfileInfo = new MethodProfilerInfo<T>(methodInfo);

                _cache.Add(methodProfileInfo.MethodProfileInfoKey, methodProfileInfo);
            }

            return methodProfileInfo.BuiltExpression(_profiledObject, methodProfileInfo, methodInfo, methodCall);
        }

        public static T Create(T objectToProfile)
        {
            ThrowNotInterfaceException();

            return (T)new ProxyProfiler<T>(objectToProfile).GetTransparentProxy();
        }

        public static IEnumerable<IMethodExecutionHistory> GetHistory(
            T profiledObject,
            string methodName,
            params Type[] methodArgsTypes)
        {
            return GetHistory(profiledObject, typeof(T).GetMethod(methodName, methodArgsTypes));
        }

        public static IEnumerable<IMethodExecutionHistory> GetHistory(T profiledObject, MethodInfo methodInfo)
        {
            ThrowNotInterfaceException();

            return GetMethodProfileInfo(methodInfo)
                .GetHistory(MethodProfilerInfo<T>.BuilLambdaObjectAndMethodKey()(profiledObject, methodInfo));
        }

        private static MethodProfilerInfo<T> GetMethodProfileInfo(MethodInfo methodInfo)
        {
            return _cache[MethodProfilerInfo<T>.CreateMethodProfileInfoKey(methodInfo)];
        }

        private static void ThrowNotInterfaceException()
        {
            if (!typeof(T).IsInterface)
            {
                throw new NotSupportedException("Generic type 'T' must be an interface");
            }
        }
    }
}
