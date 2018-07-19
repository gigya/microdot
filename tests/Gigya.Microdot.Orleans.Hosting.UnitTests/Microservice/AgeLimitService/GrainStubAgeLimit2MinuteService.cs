using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Orleans;
using Orleans.Concurrency;
using Orleans.Providers;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.AgeLimitService
{

    public interface IGrainStubAgeLimitTested
    {
        Task<bool> Activate();
        Task<TimeSpan> GetTimeStamp();
    }

    [HttpService(6746)]

    public interface IGrainStubAgeLimit2MinuteService : IGrainStubAgeLimitTested , IGrainWithIntegerKey
    {
    }


    [Reentrant]
    [StorageProvider(ProviderName = "OrleansStorage")]
    public class GrainStubAgeLimit2MinuteService : Grain<TimeSpan>, IGrainStubAgeLimit2MinuteService
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
