using System.Threading.Tasks;

using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Hosting.HttpService.Endpoints;

using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice
{
    [HttpService(6555)]
    public interface IProgrammableHealth { }

    public interface IProgrammableHealthGrain : IProgrammableHealth, IHealthStatus, IGrainWithIntegerKey
    {
        Task SetHealth(bool healthy);
    }

    public class ProgrammableHealthGrain : Grain, IProgrammableHealthGrain
    {
        private HealthStatusResult Health { get; set; }


        public async Task<HealthStatusResult> Status()
        {
            return Health;
            //return new HealthStatusResult("I am not feeling well because I suffer from cold and headache", false);
        }


        public async Task SetHealth(bool healthy)
        {
            Health = healthy ? new HealthStatusResult("I'm healthy") : new HealthStatusResult("I'm not healthy", false);
        }
    }
}