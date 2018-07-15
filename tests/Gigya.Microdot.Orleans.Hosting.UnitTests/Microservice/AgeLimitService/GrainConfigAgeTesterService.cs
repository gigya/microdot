using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Ninject;
using Orleans;
using Orleans.Concurrency;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.AgeLimitService
{

    public class AgeLimitConfigUpdatesServiceHost : CalculatorServiceHost
    {
        public Dictionary<string, string> MainConfig = new Dictionary<string, string> {
            { "OrleansConfig.defaultGrainAgeLimitInMins", "1" },
            { "OrleansConfig.GrainAgeLimits.SiteService.grainAgeLimitInMins", "1"} ,
            { "OrleansConfig.GrainAgeLimits.SiteService.grainType"          , "Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.AgeLimitService.GrainAgeLimitTestedService"}};


        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            kernel.Rebind<IConfigItemsSource, OverridableConfigItems>()
                .To<OverridableConfigItems>()
                .InSingletonScope()
                .WithConstructorArgument("data", MainConfig);


            kernel.Rebind<IConfigurationDataWatcher, ManualConfigurationEvents>()
                          .To<ManualConfigurationEvents>()
                          .InSingletonScope();


            base.Configure(kernel, commonConfig);
        }
    }


    [HttpService(6540)]
    public interface IConfigAgeTesterService
    {
        Task<bool> ChangeAgeLimitTo(int minutes);

        Task<bool> DeactivationAccured();
    }

    [HttpService(6540)]

    public interface IGrainConfigAgeTesterService : IConfigAgeTesterService, IGrainWithIntegerKey
    {
    }


    [StatelessWorker, Reentrant]
    public class GrainConfigAgeTesterService : Grain, IGrainConfigAgeTesterService
    {
        private readonly ILog _log;
        private IGrainAgeLimitTestedService _limitTested;
        private static Stopwatch _stopwatch;
        private int _expectedMinutWait;
        private static AutoResetEvent _autoResetEvent;


        public GrainConfigAgeTesterService(ILog log)
        {
            _log = log;
            _autoResetEvent = new AutoResetEvent(initialState: false);
        }

        public override Task OnActivateAsync()
        {
            //_limitTested = GrainFactory.GetGrain<IGrainAgeLimitTestedService>(new Random(Guid.NewGuid().GetHashCode()).Next());
            return base.OnActivateAsync();
        }

        public async Task<bool> ChangeAgeLimitTo(int minutes)
        {
            var key = this.GetPrimaryKeyLong().ToString();
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            var limitTested = GrainFactory.GetGrain<IGrainAgeLimitTestedService>(new Random(Guid.NewGuid().GetHashCode()).Next());


            _expectedMinutWait = 1;
            await limitTested.ChangeAgeLimit(_expectedMinutWait);

            _autoResetEvent.WaitOne(timeout: TimeSpan.FromMinutes(3)).ShouldBeTrue();


            _stopwatch = new Stopwatch();
            //_expectedMinutWait = 2;
            await _limitTested.ChangeAgeLimit(_expectedMinutWait);
            _autoResetEvent.WaitOne(timeout: TimeSpan.FromMinutes(_expectedMinutWait)).ShouldBeTrue();


            _stopwatch = new Stopwatch();
            _expectedMinutWait = 1;
            await _limitTested.ChangeAgeLimit(_expectedMinutWait);
            _autoResetEvent.WaitOne(timeout: TimeSpan.FromMinutes(_expectedMinutWait)).ShouldBeTrue();

            return true;
        }

        public Task<bool> DeactivationAccured()
        {

            _stopwatch.Stop();
            (Math.Abs(_stopwatch.Elapsed.Minutes - _expectedMinutWait) <= 1).ShouldBeTrue();

            _autoResetEvent.Set();

            return Task.FromResult(true);
        }

    }
}
