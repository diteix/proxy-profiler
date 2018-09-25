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

        public string TestReturnNoParams()
        {
            return "TestReturnNoParams";
        }

        public int TestReturnParams(int p)
        {
            return p;
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

        public async Task<int> TestReturnParamsAsync(int p)
        {
            await Task.Delay(1000);

            return p;
        }

        public void TestOutParams(out int p)
        {
            p = 1;
        }

        public string TestReturnOutParams(out int p)
        {
            p = 1;

            return "TestOutParams";
        }
    }
}
