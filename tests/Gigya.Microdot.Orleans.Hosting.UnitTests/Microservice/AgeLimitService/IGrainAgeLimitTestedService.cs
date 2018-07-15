using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Testing.Shared;
using NSubstitute;
using Orleans;
using Orleans.Concurrency;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.AgeLimitService
{
    [HttpService(6546)]

    public interface IGrainAgeLimitTestedService : IGrainWithIntegerKey
    {
        Task<bool> ChangeAgeLimit(int expectedAgeLimit);
    }


    [Reentrant]
    public class GrainAgeLimitTestedService : Grain, IGrainAgeLimitTestedService
    {
        private readonly ManualConfigurationEvents _configRefresh;
        private readonly OverridableConfigItems _configOverride;
        private readonly OrleansConfig _orleansConfig;

        public GrainAgeLimitTestedService(ManualConfigurationEvents configRefresh, OverridableConfigItems configOverride, OrleansConfig orleansConfig)
        {
            _configRefresh = configRefresh;
            _configOverride = configOverride;
            _orleansConfig = orleansConfig;
        }


        public async Task<bool> ChangeAgeLimit(int expectedAgeLimit)
        {

            if (_orleansConfig.GrainAgeLimits.FirstOrDefault().Value.GrainAgeLimitInMins == expectedAgeLimit)
                return true;

            _configOverride.SetValue("OrleansConfig.GrainAgeLimits.SiteService.grainAgeLimitInMins", expectedAgeLimit.ToString());

            var notification = await _configRefresh.ApplyChanges<OrleansConfig>();

            return true;
        }



        public override Task OnDeactivateAsync()
        {
            var key = 0;
            var tmp = GrainFactory.GetGrain<IGrainConfigAgeTesterService>(key).DeactivationAccured();

            var xxx=tmp.Result;

            return base.OnDeactivateAsync();
        }
    }
}