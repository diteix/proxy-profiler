using Console.Attributes;
using Console.Classes;
using System.Threading.Tasks;

namespace Console.Interfaces
{
    public interface ITest
    {
        [Log(typeof(Log))]
        void TestNoParams();

        [Log(typeof(Log))]
        void TestParams(int p);

        [Log(typeof(Log))]
        string TestReturnNoParams();

        [Log(typeof(Log))]
        int TestReturnParams(int p);

        [Log(typeof(Log))]
        Task TestNoParamsAsync();

        [Log(typeof(Log))]
        Task TestParamsAsync(int p);

        [Log(typeof(Log))]
        Task<string> TestReturnNoParamsAsync();

        [Log(typeof(Log))]
        Task<int> TestReturnParamsAsync(int p);

        [Log(typeof(Log))]
        void TestOutParams(out int p);

        [Log(typeof(Log))]
        string TestReturnOutParams(out int p);
    }
}
