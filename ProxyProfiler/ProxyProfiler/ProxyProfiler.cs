using ProxyProfiler.Attribute;
using ProxyProfiler.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading.Tasks;

namespace ProxyProfiler
{
    public class ProxyProfiler<T> : RealProxy
    {
        private readonly T _profiledObject;

        private static IDictionary cache = new HybridDictionary(true);

        private ProxyProfiler(T profiledObject) : base(typeof(T))
        {
            _profiledObject = profiledObject;

            if (cache.Contains(ProfileInfo.CreateCacheKey(_profiledObject)))
            {
                return;
            }

            var profileInfo = new ProfileInfo(_profiledObject);

            cache.Add(profileInfo.CacheKey, profileInfo);
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = msg as IMethodCallMessage;
            var methodInfo = methodCall.MethodBase as MethodInfo;

            var profileInfo = (ProfileInfo)cache[ProfileInfo.CreateCacheKey(_profiledObject)];

            return profileInfo.Execute(_profiledObject, methodInfo, methodCall);
        }

        public static T Create(T objectToProfile)
        {
            return (T)new ProxyProfiler<T>(objectToProfile).GetTransparentProxy();
        }

        private sealed class ProfileInfo
        {
            private IDictionary _methodsProfileInfo = new HybridDictionary(true);

            public int CacheKey { get; private set; }

            public ProfileInfo(T profiledObject)
            {
                CacheKey = CreateCacheKey(profiledObject);
            }

            public IMessage Execute(T profiledObject, MethodInfo methodInfo, IMethodCallMessage methodCall)
            {
                var profileInfo = GetMethodProfileInfo(methodInfo);

                return profileInfo.BuiltExpression(profiledObject, this, methodInfo, methodCall);
            }

            public void AddHistory(MethodInfo methodInfo, MethodProfileInfoHistory history)
            {
                GetMethodProfileInfo(methodInfo).AddHistory(history);
            }

            private MethodProfileInfo GetMethodProfileInfo(MethodInfo methodInfo)
            {
                var methodInfoKey = MethodProfileInfo.CreateMethodProfileInfoKey(methodInfo);

                MethodProfileInfo methodProfileInfo;

                if (!_methodsProfileInfo.Contains(methodInfoKey))
                {
                    _methodsProfileInfo.Add(methodInfoKey, methodProfileInfo = new MethodProfileInfo(methodInfo));
                }
                else
                {
                    methodProfileInfo = _methodsProfileInfo[methodInfoKey] as MethodProfileInfo;
                }

                return methodProfileInfo;
            }

            public static int CreateCacheKey(T profiledObject)
            {
                return profiledObject.GetHashCode();
            }

            private class MethodProfileInfo
            {
                private Expression<Func<T, ProfileInfo, MethodInfo, IMethodCallMessage, IMessage>> _lambdaExpression;
                private Func<T, ProfileInfo, MethodInfo, IMethodCallMessage, IMessage> _builtExpression;
                private LinkedList<MethodProfileInfoHistory> _methodsProfileInfoHistory = new LinkedList<MethodProfileInfoHistory>();

                public int ExecutionCount { get; private set; }

                public MethodInfo MethodInfo { get; private set; }

                public IEnumerable<AttributeInstancePair> ProfileAttributes { get; private set; }

                public Expression<Func<T, ProfileInfo, MethodInfo, IMethodCallMessage, IMessage>> LambdaExpression
                {
                    get
                    {
                        return _lambdaExpression ?? (_lambdaExpression = BuildLambdaExpression());
                    }
                }

                public Func<T, ProfileInfo, MethodInfo, IMethodCallMessage, IMessage> BuiltExpression
                {
                    get
                    {
                        return _builtExpression ?? (_builtExpression = LambdaExpression.Compile());
                    }
                }

                public MethodProfileInfo(MethodInfo methodInfo)
                {
                    MethodInfo = methodInfo;
                    ProfileAttributes = methodInfo.GetCustomAttributes(typeof(ProfilerAttribute))
                        .Select(s => new AttributeInstancePair((IProfilerAttribute)s, null));
                }

                public void AddHistory(MethodProfileInfoHistory history)
                {
                    ExecutionCount++;

                    _methodsProfileInfoHistory.AddFirst(history);
                }

                public IEnumerable<MethodCallExpression> ExecuteProfilersBefore(Expression methodInfo)
                {
                    return ExecuteProfilerAction(typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnBeforeInvoke)), methodInfo);
                }

                public IEnumerable<MethodCallExpression> ExecuteProfilersAfter(Expression methodInfo, Expression invokeResult)
                {
                    return ExecuteProfilerAction(
                        typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnAfterInvoke)),
                        methodInfo,
                        invokeResult);
                }

                public IEnumerable<MethodCallExpression> ExecuteProfilersException(Expression methodInfo, Expression exception)
                {
                    return ExecuteProfilerAction(
                        typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnInvokeException)),
                        methodInfo,
                        exception);
                }

                private IEnumerable<MethodCallExpression> ExecuteProfilerAction(
                    MethodInfo profilerAction,
                    params Expression[] args)
                {
                    var newArgs = new Expression[args.Length + 1];
                    Array.Copy(args, 0, newArgs, 1, args.Length);

                    foreach (var profiler in this.ProfileAttributes)
                    {
                        newArgs[0] = Expression.Constant(
                            profiler.ProfilerInstance ??
                            (profiler.ProfilerInstance =
                                Expression.Lambda(Expression.New(profiler.Attribute.ProfileType)).Compile().DynamicInvoke()));

                        yield return Expression.Call(Expression.Constant(profiler.Attribute), profilerAction.MakeGenericMethod(profiler.Attribute.ProfileType), newArgs);
                    }
                }

                private Expression<Func<T, ProfileInfo, MethodInfo, IMethodCallMessage, IMessage>> BuildLambdaExpression()
                {
                    var profiledObject = Expression.Parameter(typeof(T), "profiledObject");
                    var profileInfo = Expression.Parameter(typeof(ProfileInfo), "profileInfo");
                    var methodInfo = Expression.Parameter(typeof(MethodInfo), "methodInfo");
                    var methodCall = Expression.Parameter(typeof(IMethodCallMessage), "methodCall");

                    return Expression.Lambda<Func<T, ProfileInfo, MethodInfo, IMethodCallMessage, IMessage>>(
                        BuildExpression(profiledObject, profileInfo, methodInfo, methodCall),
                        profiledObject,
                        profileInfo,
                        methodInfo,
                        methodCall);
                }

                private BlockExpression BuildExpression(
                    ParameterExpression profiledObject,
                    ParameterExpression profileInfo,
                    ParameterExpression methodInfo,
                    ParameterExpression methodCall)
                {
                    var invokeResult = Expression.Parameter(typeof(object), "invokeResult");
                    var stopWatch = Expression.Parameter(typeof(Stopwatch), "stopWatch");

                    return Expression.Block(new[] { invokeResult, stopWatch },
                        Expression.Assign(stopWatch, Expression.New(typeof(Stopwatch))),
                        Expression.TryCatchFinally(
                            BuildTryBody(profiledObject, profileInfo, methodInfo, methodCall, invokeResult, stopWatch),
                            BuildFinallyBody(profileInfo, methodInfo, methodCall, invokeResult, stopWatch),
                            BuildCatchBody(methodInfo, methodCall)));
                }

                private BlockExpression BuildTryBody(
                    ParameterExpression profiledObject,
                    ParameterExpression profileInfo,
                    ParameterExpression methodInfo,
                    ParameterExpression methodCall,
                    ParameterExpression invokeResult,
                    ParameterExpression stopWatch)
                {
                    var method = typeof(MethodBase).GetMethods()
                        .FirstOrDefault(s => s.Name == nameof(MethodBase.Invoke) && s.GetParameters().Count() == 2);

                    return Expression.Block(
                        typeof(IMessage),
                            Expression.Block(this.ExecuteProfilersBefore(methodInfo)),
                            Expression.Call(stopWatch, typeof(Stopwatch).GetMethod(nameof(Stopwatch.Start))),
                            Expression.Assign(
                                invokeResult,
                                Expression.Call(
                                    methodInfo,
                                    method,
                                    profiledObject,
                                    Expression.PropertyOrField(
                                        Expression.Convert(methodCall, typeof(IMethodMessage)), nameof(IMethodMessage.Args)))),
                            Expression.New(
                                typeof(ReturnMessage).GetConstructors()[0],
                                invokeResult,
                                Expression.Constant(new object[] { }, typeof(object[])),
                                Expression.Subtract(
                                    Expression.PropertyOrField(
                                        Expression.Convert(methodCall, typeof(IMethodMessage)),
                                        nameof(IMethodMessage.ArgCount)),
                                    Expression.PropertyOrField(methodCall, nameof(IMethodCallMessage.InArgCount))),
                                Expression.PropertyOrField(
                                    Expression.Convert(methodCall, typeof(IMethodMessage)),
                                    nameof(IMethodMessage.LogicalCallContext)),
                                methodCall));
                }

                private ConditionalExpression BuildFinallyBody(
                    ParameterExpression profileInfo,
                    ParameterExpression methodInfo,
                    ParameterExpression methodCall,
                    ParameterExpression invokeResult,
                    ParameterExpression stopWatch)
                {
                    var block = Expression.Block(
                        Expression.Call(stopWatch, typeof(Stopwatch).GetMethod(nameof(Stopwatch.Stop))),
                        Expression.Block(this.ExecuteProfilersAfter(methodInfo, invokeResult)),
                        BuildAddHistory(profileInfo, methodInfo, BuildNewMethodProfileInfoHistory(methodCall, stopWatch)));

                    var method = typeof(Task).GetMethod(nameof(Task.ContinueWith), new Type[] { typeof(Action<Task>) });

                    return Expression.IfThenElse(
                        Expression.TypeEqual(invokeResult, typeof(Task)),
                        Expression.Call(
                            Expression.Convert(invokeResult, typeof(Task)),
                            method,
                            Expression.Lambda(block, Expression.Parameter(typeof(Task), "t"))),
                        block);
                }

                private CatchBlock BuildCatchBody(ParameterExpression methodInfo, ParameterExpression methodCall)
                {
                    var exceptionExpressionParameter = Expression.Parameter(typeof(Exception), "exception");

                    return Expression.Catch(
                        exceptionExpressionParameter,
                        Expression.Block(
                            typeof(IMessage),
                            Expression.Block(this.ExecuteProfilersException(methodInfo, exceptionExpressionParameter)),
                            Expression.New(
                                typeof(ReturnMessage).GetConstructors()[1],
                                exceptionExpressionParameter,
                                methodCall)));
                }

                private MethodCallExpression BuildAddHistory(
                    ParameterExpression profileInfo,
                    ParameterExpression methodInfo,
                    NewExpression history)
                {
                    var method = typeof(ProfileInfo).GetMethod(nameof(ProfileInfo.AddHistory));

                    return Expression.Call(profileInfo, method, methodInfo, history);
                }

                private NewExpression BuildNewMethodProfileInfoHistory(
                    ParameterExpression methodCall,
                    ParameterExpression stopWatch)
                {
                    return Expression.New(
                        typeof(MethodProfileInfoHistory).GetConstructors()[0],
                        Expression.PropertyOrField(
                            Expression.Convert(methodCall, typeof(IMethodMessage)), nameof(IMethodMessage.Args)),
                        Expression.PropertyOrField(stopWatch, nameof(Stopwatch.ElapsedMilliseconds)));
                }

                public static int CreateMethodProfileInfoKey(MethodInfo methodInfo)
                {
                    return methodInfo.GetHashCode();
                }
            }

            private class AttributeInstancePair
            {
                public IProfilerAttribute Attribute { get; set; }

                public object ProfilerInstance { get; set; }

                public AttributeInstancePair(IProfilerAttribute attribute, object profilerInstance)
                {
                    Attribute = attribute;
                    ProfilerInstance = profilerInstance;
                }
            }
        }

        private class MethodProfileInfoHistory
        {
            public object[] Args { get; private set; }

            public long ElapsedMilliseconds { get; private set; }

            public DateTime ExecutionDateTime { get; private set; }

            public MethodProfileInfoHistory(object[] args, long elapsedMilliseconds)
            {
                Args = args;
                ElapsedMilliseconds = elapsedMilliseconds;
                ExecutionDateTime = DateTime.Now;
            }
        }
    }
}
