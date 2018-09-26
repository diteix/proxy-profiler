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

        private static IDictionary<int, MethodProfileInfo<T>> _cache = new HybridDictionary<int, MethodProfileInfo<T>>(true);

        private ProxyProfiler(T profiledObject) : base(typeof(T))
        {
            _profiledObject = profiledObject;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = msg as IMethodCallMessage;
            var methodInfo = methodCall.MethodBase as MethodInfo;

            MethodProfileInfo<T> methodProfileInfo;

            if ((methodProfileInfo = GetMethodProfileInfo(methodInfo)) == null)
            {
                methodProfileInfo = new MethodProfileInfo<T>(methodInfo);

                _cache.Add(methodProfileInfo.MethodProfileInfoKey, methodProfileInfo);
            }

            return methodProfileInfo.BuiltExpression(_profiledObject, methodProfileInfo, methodInfo, methodCall);
        }

        public static T Create(T objectToProfile)
        {
            if (!typeof(T).IsInterface)
            {
                throw new NotSupportedException("Generic type 'T' must be an interface");
            }

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
            if (!typeof(T).IsInterface)
            {
                throw new NotSupportedException("Generic type 'T' must be an interface");
            }

            return GetMethodProfileInfo(methodInfo)
                .GetHistory(MethodProfileInfo<T>.BuilLambdaObjectAndMethodKey()(profiledObject, methodInfo));
        }

        private static MethodProfileInfo<T> GetMethodProfileInfo(MethodInfo methodInfo)
        {
            return _cache[MethodProfileInfo<T>.CreateMethodProfileInfoKey(methodInfo)];
        }
    }
}
