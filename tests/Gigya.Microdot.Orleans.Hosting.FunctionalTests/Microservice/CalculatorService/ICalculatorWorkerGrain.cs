using System;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService
{
    public interface ICalculatorWorkerGrain : IGrainWithIntegerKey
    {
        Task<int> Add(int a, int b, bool shouldThrow = false);
        Task<string[]> GetAppDomainChain(int depth);
        Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime, DateTimeOffset localDateTimeOffset);
        Task<JObject> Add(JObject jObject);
        Task<JObjectWrapper> Add(JObjectWrapper jObjectW);
        Task Do();
        Task<Wrapper> DoComplex(Wrapper wrapper);
        Task<int> DoInt(int a);
    }
}