using System;
using System.Threading.Tasks;

using Gigya.Common.Contracts.HttpService;

using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService
{
    [HttpService(6555)]
    public interface ICalculatorService
    {
        Task<int> Add(int a, int b, bool shouldThrow = false);

        [PublicEndpoint("test.calculator.getAppDomainChain")]
        Task<string[]> GetAppDomainChain(int depth);
        Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime, DateTimeOffset localDateTimeOffset);
        Task<JObject> Add(JObject jObject);
        Task<JObjectWrapper> Add(JObjectWrapper jObjectW);

        Task Do();

        Task<Wrapper> DoComplex(Wrapper wrapper);

        Task<int> DoInt(int a);        
    }
}