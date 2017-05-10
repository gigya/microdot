using System;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Orleans;
using Orleans.Concurrency;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService
{

    [StatelessWorker, Reentrant]
    public class CalculatorServiceGrain : Grain, ICalculatorServiceGrain
    {
        private ICalculatorWorkerGrain Worker { get; set; }


        public override Task OnActivateAsync()
        {
            Worker = GrainFactory.GetGrain<ICalculatorWorkerGrain>(new Random(Guid.NewGuid().GetHashCode()).Next());
            return base.OnActivateAsync();
        }


        public Task<int> Add(int a, int b, bool shouldThrow = false) { return Worker.Add(a, b, shouldThrow); }

        public Task<string[]> GetAppDomainChain(int depth) { return Worker.GetAppDomainChain(depth); }


        public Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime, DateTimeOffset localDateTimeOffset) { return Worker.ToUniversalTime(localDateTime, localDateTimeOffset); }


        public Task<JObject> Add(JObject jObject) { return Worker.Add(jObject); }


        public Task<JObjectWrapper> Add(JObjectWrapper jObjectW) { return Worker.Add(jObjectW); }


        public Task Do() { return Worker.Do(); }


        public Task<Wrapper> DoComplex(Wrapper wrapper) { return Worker.DoComplex(wrapper); }


        public Task<int> DoInt(int a) { return Worker.DoInt(a); }
        
    }

}