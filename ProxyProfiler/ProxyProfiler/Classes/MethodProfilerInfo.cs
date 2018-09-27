using ProxyProfiler.Attribute;
using ProxyProfiler.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;

namespace ProxyProfiler.Classes
{
    internal sealed class MethodProfilerInfo<T>
    {
        private Expression<Func<T, MethodProfilerInfo<T>, MethodInfo, IMethodCallMessage, IMessage>> _lambdaExpression;
        private Func<T, MethodProfilerInfo<T>, MethodInfo, IMethodCallMessage, IMessage> _builtExpression;
        private IDictionary<long, MethodProfileInfo> _methodProfilesInfos = new HybridDictionary<long, MethodProfileInfo>(true);
        private IEnumerable<AttributeInstancePair> ProfilerAttributes;

        public int MethodProfileInfoKey { get; private set; }

        public MethodInfo MethodInfo { get; private set; }

        public Expression<Func<T, MethodProfilerInfo<T>, MethodInfo, IMethodCallMessage, IMessage>> LambdaExpression
        {
            get
            {
                return _lambdaExpression ?? (_lambdaExpression = BuildLambdaExpression());
            }
        }

        public Func<T, MethodProfilerInfo<T>, MethodInfo, IMethodCallMessage, IMessage> BuiltExpression
        {
            get
            {
                return _builtExpression ?? (_builtExpression = LambdaExpression.Compile());
            }
        }

        public MethodProfilerInfo(MethodInfo methodInfo)
        {
            MethodProfileInfoKey = CreateMethodProfileInfoKey(methodInfo);
            MethodInfo = methodInfo;
            ProfilerAttributes = methodInfo.GetCustomAttributes(typeof(ProfilerAttribute))
                .Select(s => new AttributeInstancePair((IProfilerAttribute)s, null));
        }

        public void AddHistory(MethodProfileInfoHistory history, long historyKey)
        {
            MethodProfileInfo methodProfileInfo;

            if ((methodProfileInfo = _methodProfilesInfos[historyKey]) == null)
            {
                methodProfileInfo = new MethodProfileInfo();
                _methodProfilesInfos.Add(historyKey, methodProfileInfo);
            }

            methodProfileInfo.ExecutionCount++;
            methodProfileInfo.History.AddFirst(history);
        }

        public IEnumerable<IMethodExecutionHistory> GetHistory(long historyKey)
        {
            return _methodProfilesInfos[historyKey].History;
        }

        public IEnumerable<Expression> ExecuteProfilersBefore(ParameterExpression methodInfo, ParameterExpression methodCall)
        {
            return ExecuteProfilerAction(
                typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnBeforeInvoke)),
                methodInfo,
                Expression.PropertyOrField(
                    Expression.Convert(methodCall, typeof(IMethodMessage)), nameof(IMethodMessage.Args)));
        }

        public IEnumerable<Expression> ExecuteProfilersAfter(
            ParameterExpression methodInfo,
            ParameterExpression methodCall,
            ParameterExpression args,
            ParameterExpression invokeResult)
        {
            return ExecuteProfilerAction(
                typeof(IProfilerAttribute).GetMethod(nameof(IProfilerAttribute.OnAfterInvoke)),
                methodInfo,
                Expression.PropertyOrField(
                    Expression.Convert(methodCall, typeof(IMethodMessage)), nameof(IMethodMessage.Args)),
                args,
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
                        Expression.Lambda(Expression.New(profiler.Attribute.ProfilerType)).Compile().DynamicInvoke()));

                yield return Expression.Call(Expression.Constant(profiler.Attribute), profilerAction.MakeGenericMethod(profiler.Attribute.ProfilerType), newArgs);
            }
        }

        private Expression<Func<T, MethodProfilerInfo<T>, MethodInfo, IMethodCallMessage, IMessage>> BuildLambdaExpression()
        {
            var profiledObject = Expression.Parameter(typeof(T), "profiledObject");
            var methodProfileInfo = Expression.Parameter(typeof(MethodProfilerInfo<T>), "methodProfileInfo");
            var methodInfo = Expression.Parameter(typeof(MethodInfo), "methodInfo");
            var methodCall = Expression.Parameter(typeof(IMethodCallMessage), "methodCall");

            return Expression.Lambda<Func<T, MethodProfilerInfo<T>, MethodInfo, IMethodCallMessage, IMessage>>(
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
            var args = Expression.Parameter(typeof(object[]), "args");
            var invokeResult = Expression.Parameter(typeof(object), "invokeResult");
            var stopWatch = Expression.Parameter(typeof(Stopwatch), "stopWatch");

            return Expression.Block(new[] { args, invokeResult, stopWatch },
                Expression.Assign(stopWatch, Expression.New(typeof(Stopwatch))),
                Expression.Assign(invokeResult, Expression.Constant(new object(), typeof(object))),
                Expression.TryCatchFinally(
                    BuildTryBody(profiledObject, methodInfo, methodCall, args, invokeResult, stopWatch),
                    BuildFinallyBody(profiledObject, methodProfileInfo, methodInfo, methodCall, args, invokeResult, stopWatch),
                    BuildCatchBody(methodInfo, methodCall)));
        }

        private BlockExpression BuildTryBody(
            ParameterExpression profiledObject,
            ParameterExpression methodInfo,
            ParameterExpression methodCall,
            ParameterExpression args,
            ParameterExpression invokeResult,
            ParameterExpression stopWatch)
        {
            var method = typeof(MethodBase).GetMethods()
                .FirstOrDefault(s => s.Name == nameof(MethodBase.Invoke) && s.GetParameters().Count() == 2);

            return Expression.Block(
                typeof(IMessage),
                Expression.Block(this.ExecuteProfilersBefore(methodInfo, methodCall)),
                Expression.Assign(
                    args,
                    Expression.PropertyOrField(
                        Expression.Convert(methodCall, typeof(IMethodMessage)), nameof(IMethodMessage.Args))),
                Expression.Call(stopWatch, typeof(Stopwatch).GetMethod(nameof(Stopwatch.Start))),
                Expression.Assign(
                    invokeResult,
                    Expression.Call(
                        methodInfo,
                        method,
                        profiledObject,
                        args)),
                Expression.New(
                    typeof(ReturnMessage).GetConstructors()[0],
                    invokeResult,
                    args,
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
            ParameterExpression args,
            ParameterExpression invokeResult,
            ParameterExpression stopWatch)
        {
            var block = Expression.Block(
                Expression.Call(stopWatch, typeof(Stopwatch).GetMethod(nameof(Stopwatch.Stop))),
                Expression.Block(this.ExecuteProfilersAfter(methodInfo, methodCall, args, invokeResult)),
                BuildAddHistory(
                    profiledObject,
                    methodProfileInfo,
                    methodInfo,
                    BuildNewMethodProfileInfoHistory(methodCall, args, stopWatch)));

            var method = typeof(Task).GetMethod(nameof(Task.ContinueWith), new Type[] { typeof(Action<Task>) });

            return Expression.IfThenElse(
                Expression.TypeIs(invokeResult, typeof(Task)),
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
            var methodAddHistory = typeof(MethodProfilerInfo<T>).GetMethod(nameof(MethodProfilerInfo<T>.AddHistory));

            return Expression.Call(
                methodProfileInfo,
                methodAddHistory,
                history,
                BuildObjectAndMethodKey(profiledObject, methodInfo));
        }

        private NewExpression BuildNewMethodProfileInfoHistory(
            ParameterExpression methodCall,
            ParameterExpression args,
            ParameterExpression stopWatch)
        {
            return Expression.New(
                typeof(MethodProfileInfoHistory).GetConstructors()[0],
                Expression.PropertyOrField(
                    Expression.Convert(methodCall, typeof(IMethodMessage)), nameof(IMethodMessage.Args)),
                args,
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
            var methodCreateObjectProfileInfoKey = typeof(MethodProfilerInfo<T>)
                .GetMethod(nameof(MethodProfilerInfo<T>.CreateObjectProfileInfoKey));
            var methodCreateMethodProfileInfoKey = typeof(MethodProfilerInfo<T>)
                .GetMethod(nameof(MethodProfilerInfo<T>.CreateMethodProfileInfoKey));

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

        internal sealed class AttributeInstancePair
        {
            public IProfilerAttribute Attribute { get; set; }

            public object ProfilerInstance { get; set; }

            public AttributeInstancePair(IProfilerAttribute attribute, object profilerInstance)
            {
                Attribute = attribute;
                ProfilerInstance = profilerInstance;
            }
        }

        internal sealed class MethodProfileInfo
        {
            public int ExecutionCount { get; set; }

            public LinkedList<MethodProfileInfoHistory> History { get; private set; } = new LinkedList<MethodProfileInfoHistory>();
        }

        internal sealed class MethodProfileInfoHistory : IMethodExecutionHistory
        {
            public object[] BeforeInvokeArgs { get; private set; }

            public object[] AfterInvokeArgs { get; private set; }

            public long ElapsedMilliseconds { get; private set; }

            public DateTime ExecutionDateTime { get; private set; }

            public MethodProfileInfoHistory(object[] beforeInvokeArgs, object[] afterInvokeArgs, long elapsedMilliseconds)
            {
                BeforeInvokeArgs = beforeInvokeArgs;
                AfterInvokeArgs = afterInvokeArgs;
                ElapsedMilliseconds = elapsedMilliseconds;
                ExecutionDateTime = DateTime.Now;
            }
        }
    }
}
