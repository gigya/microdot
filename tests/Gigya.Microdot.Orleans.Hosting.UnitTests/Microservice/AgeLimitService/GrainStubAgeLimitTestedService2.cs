using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Orleans;
using Orleans.Concurrency;
using Orleans.Providers;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.AgeLimitService
{

    [HttpService(7146)]

    public interface IGrainStubAgeLimitDefaultTime1MinuteService : IGrainStubAgeLimitTested,IGrainWithIntegerKey
    {
    }


    [Reentrant]
    [StorageProvider(ProviderName = "OrleansStorage")]
    public class GrainStubAgeLimitDefaultTime1MinuteService : Grain<TimeSpan>, IGrainStubAgeLimitDefaultTime1MinuteService
    {
        private Stopwatch _stopWatch;

        public async Task<bool> Activate()
        {
            _stopWatch = new Stopwatch();
            _stopWatch.Start();

            State = _stopWatch.Elapsed;
            await this.WriteStateAsync();

            return true;
        }

        public async Task<TimeSpan> GetTimeStamp()
        {
            await ReadStateAsync();
            return State;
        }

        public override Task OnDeactivateAsync()
        {
            _stopWatch.Stop();
            State = _stopWatch.Elapsed;
            WriteStateAsync();

            return base.OnDeactivateAsync();
        }
    }
}