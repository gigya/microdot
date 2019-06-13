using System.Threading.Tasks;

using Gigya.Common.Contracts.HttpService;

namespace Gigya.Microdot.UnitTests
{
    [HttpService(5555)]
    public interface IDemoService
    {
        Task DoSomething();
        Task<string> ToUpper(string str);
        Task<ulong> Increment(ulong val);
        Task SendEnum(TestEnum value);
        Task<int> IncrementInt(int val);
    }

    [HttpService(6555, UseHttps = true)]
    public interface IDemoServiceSecure
    {
        Task DoSomething();
        Task<string> ToUpper(string str);
        Task<ulong> Increment(ulong val);
        Task SendEnum(TestEnum value);
    }

    public enum TestEnum
    {
        Enval1,
        Enval2,
        Enval3
    }

}