using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.Measurement;

using Newtonsoft.Json.Linq;

using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService
{
    public class CalculatorWorkerGrain : Grain, ICalculatorWorkerGrain
    {
      
        private ILog Log { get; set; }


        public CalculatorWorkerGrain(ILog log)
        {
            Log = log;

        }


        public async Task<int> Add(int a, int b, bool shouldThrow)
        {
            using (RequestTimings.Current.DataSource.Hades.Delete.Measure())
                await Task.Delay(100);

            Log.Info(_ => _("Server: Adding {a} + {b}"));

            if (shouldThrow)
                throw new RequestException("You have request to throw an exception. Now catch!");

            return a + b;
        }


     

        public async Task<string[]> GetAppDomainChain(int depth)
        {
            var current = new[] { AppDomain.CurrentDomain.FriendlyName };

            if (depth == 1)
                return current;

            var chain = await GrainFactory.GetGrain<ICalculatorServiceGrain>(depth).GetAppDomainChain(depth - 1);

            return current.Concat(chain).ToArray();
        }


        public async Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime, DateTimeOffset localDateTimeOffset)
        {
            if (localDateTime.Kind != DateTimeKind.Local)
                throw new RequestException("localDateTime must be DateTimeKind.Local");

            if (localDateTimeOffset.Offset == TimeSpan.Zero)
                throw new RequestException("localDateTimeOffset must be in UTC offset");

            return Tuple.Create(localDateTime.ToUniversalTime(), localDateTimeOffset.ToUniversalTime());
        }


        public async Task<JObject> Add(JObject jObject)
        {
            jObject["c"] = jObject["a"].Value<int>() + jObject["b"].Value<int>();
            return jObject;
        }


        public async Task<JObjectWrapper> Add(JObjectWrapper jObjectW)
        {
            jObjectW.jObject["c"] = jObjectW.jObject["a"].Value<int>() + jObjectW.jObject["b"].Value<int>();
            return jObjectW;
        }


        public async Task Do() { }

        public async Task<Wrapper> DoComplex(Wrapper wrapper) { return wrapper; }

        public async Task<int> DoInt(int a) { return a; }

    }
}