using Console.Interfaces;
using System.Threading.Tasks;

namespace Console.Classes
{
    public class Test : ITest
    {
        public void TestNoParams()
        {

        }

        public void TestParams(int p)
        {

        }

        public void TestMoreParams(int p, int p2)
        {

        }

        public string TestReturnNoParams()
        {
            return "TestReturnNoParams";
        }

        public string TestReturnParams(int p)
        {
            return "TestReturnParams";
        }

        public string TestReturnMoreParams(int p, int p2)
        {
            return "TestReturnMoreParams";
        }

        public async Task TestNoParamsAsync()
        {
            await Task.Delay(1000);
        }

        public async Task TestParamsAsync(int p)
        {
            await Task.Delay(1000);
        }

        public async Task<string> TestReturnNoParamsAsync()
        {
            await Task.Delay(1000);

            return "TestReturnNoParamsAsync";
        }

        public async Task<string> TestReturnParamsAsync(int p)
        {
            await Task.Delay(1000);

            return "TestReturnParamsAsync";
        }

        public void TestOutParams(out int p)
        {
            p = 1;
        }

        public void TestOutMoreParams(int p, out int p2)
        {
            p2 = p;
        }

        public string TestReturnOutParams(out int p)
        {
            p = 1;

            return "TestReturnOutParams";
        }

        public string TestReturnOutMoreParams(int p, out int p2)
        {
            p2 = p;

            return "TestReturnOutMoreParams";
        }

        public void TestRefParams(ref int p)
        {
            p = 1;
        }

        public void TestRefMoreParams(int p, ref int p2)
        {
            p2 = p;
        }

        public string TestReturnRefParams(ref int p)
        {
            p = 1;

            return "TestReturnRefParams";
        }

        public string TestReturnRefMoreParams(int p, ref int p2)
        {
            p2 = p;

            return "TestReturnRefMoreParams";
        }

        public int TestReturnDefaultParams(int p = 2)
        {
            return p;
        }
    }
}
