using Console.Classes;
using Console.Interfaces;
using ProxyProfiler;

namespace Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var testProfiler = ProxyProfiler<ITest>.Create(new Test());

            WriteMethod(nameof(ITest.TestNoParams));
            testProfiler.TestNoParams();

            WriteMethod(nameof(ITest.TestParams));
            testProfiler.TestParams(1);

            WriteMethod(nameof(ITest.TestMoreParams));
            testProfiler.TestMoreParams(1, 2);

            WriteMethod(nameof(ITest.TestReturnNoParams));
            WriteReturn(testProfiler.TestReturnNoParams());

            WriteMethod(nameof(ITest.TestReturnParams));
            WriteReturn(testProfiler.TestReturnParams(1));

            WriteMethod(nameof(ITest.TestReturnMoreParams));
            WriteReturn(testProfiler.TestReturnMoreParams(1, 2));

            WriteMethod(nameof(ITest.TestNoParamsAsync));
            testProfiler.TestNoParamsAsync().Wait();

            WriteMethod(nameof(ITest.TestParamsAsync));
            testProfiler.TestParamsAsync(1).Wait();

            WriteMethod(nameof(ITest.TestReturnNoParamsAsync));
            WriteReturn(testProfiler.TestReturnNoParamsAsync().Result);

            WriteMethod(nameof(ITest.TestReturnParamsAsync));
            WriteReturn(testProfiler.TestReturnParamsAsync(1).Result);

            int p;

            WriteMethod(nameof(ITest.TestOutParams));
            testProfiler.TestOutParams(out p);
            WriteOut(p);

            p = 0;

            WriteMethod(nameof(ITest.TestOutMoreParams));
            testProfiler.TestOutMoreParams(1, out p);
            WriteOut(p);

            p = 0;

            WriteMethod(nameof(ITest.TestReturnOutParams));
            WriteReturn(testProfiler.TestReturnOutParams(out p));
            WriteOut(p);

            p = 0;

            WriteMethod(nameof(ITest.TestReturnOutMoreParams));
            WriteReturn(testProfiler.TestReturnOutMoreParams(1, out p));
            WriteOut(p);

            p = 0;

            WriteMethod(nameof(ITest.TestRefParams));
            testProfiler.TestRefParams(ref p);
            WriteRef(p);

            p = 0;

            WriteMethod(nameof(ITest.TestRefMoreParams));
            testProfiler.TestRefMoreParams(1, ref p);
            WriteRef(p);

            p = 0;

            WriteMethod(nameof(ITest.TestReturnRefParams));
            WriteReturn(testProfiler.TestReturnRefParams(ref p));
            WriteRef(p);

            p = 0;

            WriteMethod(nameof(ITest.TestReturnRefMoreParams));
            WriteReturn(testProfiler.TestReturnRefMoreParams(1, ref p));
            WriteRef(p);

            WriteMethod(nameof(ITest.TestReturnDefaultParams));
            WriteReturn(testProfiler.TestReturnDefaultParams().ToString());

            WriteMethod(nameof(ITest.TestReturnDefaultParams));
            WriteReturn(testProfiler.TestReturnDefaultParams(1).ToString());

            System.Console.ReadKey();
        }

        private static void WriteMethod(string methodName)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("Testing method " + methodName);
        }

        private static void WriteReturn(string returnedValue)
        {
            System.Console.WriteLine("Returned value: " + returnedValue);
        }

        private static void WriteOut(int outValue)
        {
            System.Console.WriteLine("Out value: " + outValue);
        }

        private static void WriteRef(int refValue)
        {
            System.Console.WriteLine("Ref value: " + refValue);
        }
    }
}
