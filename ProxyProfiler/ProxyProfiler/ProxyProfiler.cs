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
    public class ProxyProfiler<T> : RealProxy where T : class
    {
        private readonly T _profiledObject;

        private static IDictionary cache = new HybridDictionary(true);

        private ProxyProfiler(T profiledObject) : base(typeof(T))
        {
            _profiledObject = profiledObject;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = msg as IMethodCallMessage;
            var methodInfo = methodCall.MethodBase as MethodInfo;

            MethodProfileInfo methodProfileInfo;

            if ((methodProfileInfo = GetMethodProfileInfo(methodInfo)) == null)
            {
                methodProfileInfo = new MethodProfileInfo(methodInfo);

                cache.Add(methodProfileInfo.MethodProfileInfoKey, methodProfileInfo);
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

        public static IEnumerable<IMethodExecutionHistory> GetHistory(T profiledObject, string methodName, params Type[] methodArgsTypes)
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
                .GetHistory(MethodProfileInfo.BuilLambdaObjectAndMethodKey()(profiledObject, methodInfo));
        }

        private static MethodProfileInfo GetMethodProfileInfo(MethodInfo methodInfo)
        {
            return cache[MethodProfileInfo.CreateMethodProfileInfoKey(methodInfo)] as MethodProfileInfo;
        }

        private sealed class MethodProfileInfo
        {
            private Expression<Func<T, MethodProfileInfo, MethodInfo, IMethodCallMessage, IMessage>> _lambdaExpression;
            private Func<T, MethodProfileInfo, MethodInfo, IMethodCallMessage, IMessage> _builtExpression;
            private IDictionary _methodProfilesInfoHistory = new HybridDictionary(true);

            public int MethodProfileInfoKey { get; private set; }

            public int ExecutionCount { get; private set; }

            public MethodInfo MethodInfo { get; private set; }

            public IEnumerable<AttributeInstancePair> ProfilerAttributes { get; private set; }

            public Expression<Func<T, MethodProfileInfo, MethodInfo, IMethodCallMessage, IMessage>> LambdaExpression
            {
                get
                {
                    return _lambdaExpression ?? (_lambdaExpression = BuildLambdaExpression());
                }
            }

            public Func<T, MethodProfileInfo, MethodInfo, IMethodCallMessage, IMessage> BuiltExpression
            {
                get
                {
                    return _builtExpression ?? (_builtExpression = LambdaExpression.Compile());
                }
            }

            public MethodProfileInfo(MethodInfo methodInfo)
            {
                MethodProfileInfoKey = CreateMethodProfileInfoKey(methodInfo);
                MethodInfo = methodInfo;
                ProfilerAttributes = methodInfo.GetCustomAttributes(typeof(ProfilerAttribute))
                    .Select(s => new AttributeInstancePair((IProfilerAttribute)s, null));
            }

            public void AddHistory(MethodProfileInfoHistory history, long historyKey)
            {
                ExecutionCount++;

                LinkedList<MethodProfileInfoHistory> historic;

                if ((historic = GetHistory(historyKey)) == null)
                {
                    historic = new LinkedList<MethodProfileInfoHistory>();
                    _methodProfilesInfoHistory.Add(historyKey, historic);
                }

                historic.AddFirst(history);
            }

            public LinkedList<MethodProfileInfoHistory> GetHistory(long historyKey)
            {
                return _methodProfilesInfoHistory[historyKey] as LinkedList<MethodProfileInfoHistory>;
            }

            public IEnumerable<Expression> ExecuteProfilersBefore(Expression methodInfo)
            {
                return ExecuteProfilerAction(typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnBeforeInvoke)), methodInfo);
            }

            public IEnumerable<Expression> ExecuteProfilersAfter(Expression methodInfo, Expression invokeResult)
            {
                return ExecuteProfilerAction(
                    typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnAfterInvoke)),
                    methodInfo,
                    invokeResult);
            }

            public IEnumerable<Expression> ExecuteProfilersException(Expression methodInfo, Expression exception)
            {
                return ExecuteProfilerAction(
                    typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnInvokeException)),
                    methodInfo,
                    exception);
            }

            private IEnumerable<Expression> ExecuteProfilerAction(
                MethodInfo profilerAction,
                params Expression[] args)
            {
                if (!this.ProfilerAttributes.Any())
                {
                    yield return Expression.Empty();
                    yield break;
                }

                var newArgs = new Expression[args.Length + 1];
                Array.Copy(args, 0, newArgs, 1, args.Length);

                foreach (var profiler in this.ProfilerAttributes)
                {
                    newArgs[0] = Expression.Constant(
                        profiler.ProfilerInstance ??
                        (profiler.ProfilerInstance =
                            Expression.Lambda(Expression.New(profiler.Attribute.ProfileType)).Compile().DynamicInvoke()));

                    yield return Expression.Call(Expression.Constant(profiler.Attribute), profilerAction.MakeGenericMethod(profiler.Attribute.ProfileType), newArgs);
                }
            }

            private Expression<Func<T, MethodProfileInfo, MethodInfo, IMethodCallMessage, IMessage>> BuildLambdaExpression()
            {
                var profiledObject = Expression.Parameter(typeof(T), "profiledObject");
                var methodProfileInfo = Expression.Parameter(typeof(MethodProfileInfo), "methodProfileInfo");
                var methodInfo = Expression.Parameter(typeof(MethodInfo), "methodInfo");
                var methodCall = Expression.Parameter(typeof(IMethodCallMessage), "methodCall");

                return Expression.Lambda<Func<T, MethodProfileInfo, MethodInfo, IMethodCallMessage, IMessage>>(
                    BuildExpression(profiledObject, methodProfileInfo, methodInfo, methodCall),
                    profiledObject,
                    methodProfileInfo,
                    methodInfo,
                    methodCall);
            }

            private BlockExpression BuildExpression(
                ParameterExpression profiledObject,
                ParameterExpression methodProfileInfo,
                ParameterExpression methodInfo,
                ParameterExpression methodCall)
            {
                var invokeResult = Expression.Parameter(typeof(object), "invokeResult");
                var stopWatch = Expression.Parameter(typeof(Stopwatch), "stopWatch");

                return Expression.Block(new[] { invokeResult, stopWatch },
                    Expression.Assign(stopWatch, Expression.New(typeof(Stopwatch))),
                    Expression.TryCatchFinally(
                        BuildTryBody(profiledObject, methodInfo, methodCall, invokeResult, stopWatch),
                        BuildFinallyBody(profiledObject, methodProfileInfo, methodInfo, methodCall, invokeResult, stopWatch),
                        BuildCatchBody(methodInfo, methodCall)));
            }

            private BlockExpression BuildTryBody(
                ParameterExpression profiledObject,
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
                ParameterExpression profiledObject,
                ParameterExpression methodProfileInfo,
                ParameterExpression methodInfo,
                ParameterExpression methodCall,
                ParameterExpression invokeResult,
                ParameterExpression stopWatch)
            {
                var block = Expression.Block(
                    Expression.Call(stopWatch, typeof(Stopwatch).GetMethod(nameof(Stopwatch.Stop))),
                    Expression.Block(this.ExecuteProfilersAfter(methodInfo, invokeResult)),
                    BuildAddHistory(profiledObject, methodProfileInfo, methodInfo, BuildNewMethodProfileInfoHistory(methodCall, stopWatch)));

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
                ParameterExpression profiledObject,
                ParameterExpression methodProfileInfo,
                ParameterExpression methodInfo,
                NewExpression history)
            {
                var methodAddHistory = typeof(MethodProfileInfo).GetMethod(nameof(MethodProfileInfo.AddHistory));

                return Expression.Call(
                    methodProfileInfo,
                    methodAddHistory,
                    history,
                    BuildObjectAndMethodKey(profiledObject, methodInfo));
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

            public static int CreateObjectProfileInfoKey(T profiledObject)
            {
                return profiledObject.GetHashCode();
            }

            public static Func<T, MethodInfo, long> BuilLambdaObjectAndMethodKey()
            {
                var profiledObject = Expression.Parameter(typeof(T), "profiledObject");
                var methodInfo = Expression.Parameter(typeof(MethodInfo), "methodInfo");

                return Expression.Lambda<Func<T, MethodInfo, long>>(
                    BuildObjectAndMethodKey(profiledObject, methodInfo),
                    profiledObject,
                    methodInfo).Compile();
            }

            private static BinaryExpression BuildObjectAndMethodKey(
                ParameterExpression profiledObject,
                ParameterExpression methodInfo)
            {
                var methodCreateObjectProfileInfoKey = typeof(MethodProfileInfo)
                    .GetMethod(nameof(MethodProfileInfo.CreateObjectProfileInfoKey));
                var methodCreateMethodProfileInfoKey = typeof(MethodProfileInfo)
                    .GetMethod(nameof(MethodProfileInfo.CreateMethodProfileInfoKey));

                return Expression.Add(
                        Expression.LeftShift(
                            Expression.Convert(
                                Expression.Call(
                                    methodCreateObjectProfileInfoKey,
                                    profiledObject),
                                typeof(long)),
                            Expression.Constant(32)),
                        Expression.Convert(
                            Expression.Call(methodCreateMethodProfileInfoKey, methodInfo),
                            typeof(long)));
            }

            public sealed class AttributeInstancePair
            {
                public IProfilerAttribute Attribute { get; set; }

                public object ProfilerInstance { get; set; }

                public AttributeInstancePair(IProfilerAttribute attribute, object profilerInstance)
                {
                    Attribute = attribute;
                    ProfilerInstance = profilerInstance;
                }
            }

            public sealed class MethodProfileInfoHistory : IMethodExecutionHistory
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

            public static IEnumerable<Expression> AsNullIfEmpty(IEnumerable<Expression> items)
            {
                if (items == null || !items.Any())
                {
                    return null;
                }

                return items;
            }
        }
    }
}
