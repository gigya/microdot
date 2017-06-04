using System.Threading.Tasks;

using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.UnitTests.ServiceProxyTests;

namespace Gigya.Microdot.UnitTests
{
    [HttpService(5555,  Name = AbstractServiceProxyTest.SERVICE_NAME)]
    public interface IDemoService
    {
        Task DoSomething();
        Task<string> ToUpper(string str);
        Task<ulong> Increment(ulong val);
        Task SendEnum(TestEnum value);
        Task<int> IncrementInt(int val);
    }

    [HttpService(6555, UseHttps = true, Name = AbstractServiceProxyTest.SERVICE_NAME)]
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