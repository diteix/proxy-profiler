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
        void TestMoreParams(int p, int p2);

        [Log(typeof(Log))]
        string TestReturnNoParams();

        [Log(typeof(Log))]
        string TestReturnParams(int p);

        [Log(typeof(Log))]
        string TestReturnMoreParams(int p, int p2);

        [Log(typeof(Log))]
        Task TestNoParamsAsync();

        [Log(typeof(Log))]
        Task TestParamsAsync(int p);

        [Log(typeof(Log))]
        Task<string> TestReturnNoParamsAsync();

        [Log(typeof(Log))]
        Task<string> TestReturnParamsAsync(int p);

        [Log(typeof(Log))]
        void TestOutParams(out int p);

        [Log(typeof(Log))]
        void TestOutMoreParams(int p, out int p2);

        [Log(typeof(Log))]
        string TestReturnOutParams(out int p);

        [Log(typeof(Log))]
        string TestReturnOutMoreParams(int p, out int p2);

        [Log(typeof(Log))]
        void TestRefParams(ref int p);

        [Log(typeof(Log))]
        void TestRefMoreParams(int p, ref int p2);

        [Log(typeof(Log))]
        string TestReturnRefParams(ref int p);

        [Log(typeof(Log))]
        string TestReturnRefMoreParams(int p, ref int p2);

        [Log(typeof(Log))]
        int TestReturnDefaultParams(int p = 2);
    }
}
