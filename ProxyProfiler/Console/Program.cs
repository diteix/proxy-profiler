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

            testProfiler.TestNoParamsAsync().Wait();
        }
    }
}
