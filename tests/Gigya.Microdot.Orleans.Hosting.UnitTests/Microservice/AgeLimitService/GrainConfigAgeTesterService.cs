//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Threading.Tasks;
//using Gigya.Common.Contracts.HttpService;
//using Gigya.Microdot.Configuration;
//using Gigya.Microdot.Fakes;
//using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
//using Ninject;
//using Orleans;
//using Orleans.Concurrency;
//using Orleans.Storage;
//
//namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.AgeLimitService
//{
//    // #ORLEANS20
//    //public class AgeLimitConfigUpdatesServiceHost : CalculatorServiceHost
//    //{
//    //    public Dictionary<string, string> MainConfig = new Dictionary<string, string> {
//    //        { "OrleansConfig.defaultGrainAgeLimitInMins", "1" }};
//    //
//    //
//    //    protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
//    //    {
//    //        kernel.Rebind<IConfigItemsSource, OverridableConfigItems>()
//    //            .To<OverridableConfigItems>()
//    //            .InSingletonScope()
//    //            .WithConstructorArgument("data", MainConfig);
//    //
//    //
//    //        kernel.Rebind<IConfigurationDataWatcher, ManualConfigurationEvents>()
//    //                      .To<ManualConfigurationEvents>()
//    //                      .InSingletonScope();
//    //
//    //
//    //        kernel.Rebind<OrleansCodeConfig>().ToConstant(new OrleansCodeConfig
//    //        {
//    //            StorageProviderTypeFullName = typeof(MemoryStorage).GetTypeInfo().FullName,
//    //            StorageProviderName = "OrleansStorage"
//    //
//    //        }).InSingletonScope();
//    //
//    //
//    //        base.Configure(kernel, commonConfig);
//    //    }
//    //}
//    //
//    //
//    //[HttpService(6540)]
//    //public interface IConfigAgeTesterService
//    //{
//    //    Task<bool> SetDefaultAgeLimit();
//    //    Task<bool> ValidateTimestamps();
//    //}
//    //
//    //[HttpService(6540)]
//    //
//    //public interface IGrainConfigAgeTesterService : IConfigAgeTesterService, IGrainWithIntegerKey
//    //{
//    //}
//    //
//    //
//    //
//    //[Reentrant]
//    //public class GrainConfigAgeTesterService : Grain, IGrainConfigAgeTesterService
//    //{
//    //    private readonly ManualConfigurationEvents _configRefresh;
//    //    private readonly OverridableConfigItems _configOverride;
//    //    private readonly OrleansConfig _orleansConfig;
//    //
//    //
//    //    public GrainConfigAgeTesterService(ManualConfigurationEvents configRefresh, OverridableConfigItems configOverride, OrleansConfig orleansConfig)
//    //    {
//    //        _configRefresh = configRefresh;
//    //        _configOverride = configOverride;
//    //        _orleansConfig = orleansConfig;
//    //    }
//    //
//    //    public async Task<bool> SetDefaultAgeLimit()
//    //    {
//    //        var expected = "2";
//    //        _configOverride.SetValue("OrleansConfig.GrainAgeLimits.SiteService.grainAgeLimitInMins", "2");
//    //        _configOverride.SetValue("OrleansConfig.GrainAgeLimits.SiteService.grainType", typeof(GrainStubAgeLimit2MinuteService).FullName);
//    //
//    //
//    //        var notification = await _configRefresh.ApplyChanges<OrleansConfig>();
//    //        await Task.Delay(TimeSpan.FromSeconds(1));
//    //
//    //
//    //        var minteService2 = GrainFactory.GetGrain<IGrainStubAgeLimit2MinuteService>(0);
//    //        await minteService2.Activate();
//    //
//    //        var default1MinuteService = GrainFactory.GetGrain<IGrainStubAgeLimitDefaultTime1MinuteService>(0);
//    //        await default1MinuteService.Activate();
//    //
//    //        return true;
//    //    }
//    //
//    //    public async Task<bool> ValidateTimestamps()
//    //    {
//    //        var timestamp = await GrainFactory.GetGrain<IGrainStubAgeLimit2MinuteService>(0).GetTimeStamp();
//    //
//    //        if (timestamp > new TimeSpan(0, 3, 5))
//    //        {
//    //            throw new Exception("Should be less than 2.5 minute.");
//    //        }
//    //
//    //        timestamp = await GrainFactory.GetGrain<IGrainStubAgeLimitDefaultTime1MinuteService>(0).GetTimeStamp();
//    //
//    //        if (timestamp > new TimeSpan(0, 2, 0))
//    //        {
//    //            throw new Exception("Should be less than 1 minute.");
//    //        }
//    //
//    //
//    //        return true;
//    //    }
//    //}
//}
