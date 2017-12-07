using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Attributes;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Caching
{
    [TestFixture]
    public class CachingProxyTests
    {
        const string FirstResult  = "First Result";
        const string SecondResult = "Second Result";

        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _kernel;
        private ICachingTestService _proxy;
        private ICachingTestService _serviceMock;
        private DateTime _now;
        private string _serviceResult;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _configDic = new Dictionary<string,string>();
            _kernel = new TestingKernel<ConsoleLog>(mockConfig: _configDic);

            _kernel.Rebind(typeof(CachingProxyProvider<>))
                .ToSelf()      
                .InTransientScope();
        }

        [SetUp]
        public void Setup()
        {            
            SetupServiceMock();
            SetupDateTime();

            _proxy = _kernel.Get<ICachingTestService>();
        }

        [TearDown]
        public void TearDown()
        {
            _kernel.Get<AsyncCache>().Clear();
        }

        private void SetupServiceMock()
        {
            _serviceMock = Substitute.For<ICachingTestService>();
            _serviceMock.CallService().Returns(_ => Task.FromResult(_serviceResult));
            _serviceResult = FirstResult;
            var serviceProxyMock = Substitute.For<IServiceProxyProvider<ICachingTestService>>();
            serviceProxyMock.Client.Returns(_serviceMock);
            _kernel.Rebind<IServiceProxyProvider<ICachingTestService>>().ToConstant(serviceProxyMock);
        }

        private void SetupDateTime()
        {
            _now = DateTime.UtcNow;
            var dateTimeMock = Substitute.For<IDateTime>();
            dateTimeMock.UtcNow.Returns(_=>_now);
            _kernel.Rebind<IDateTime>().ToConstant(dateTimeMock);
        }

        [Test]
        public async Task CachingEnabledByDefault()
        {
            await ClearCahingPolicyConfig();
            await ServiceResultShouldBeCached();
        }

        [Test]
        public async Task CachingDisabledByConfiguration()
        {            
            await SetCachingPolicyConfig(new[] {"Enabled", "false"});
            await ServiceResultShouldNotBeCached();
        }

        [Test]
        public async Task CachingDisabledByMethodConfiguration()
        {
            await SetCachingPolicyConfig(new[] { "Methods.CallService.Enabled", "false" });
            await ServiceResultShouldNotBeCached();
        }

        [Test]
        public async Task CachingOfOtherMathodDisabledByConfiguration()
        {
            await SetCachingPolicyConfig(new[] { "Methods.OtherMethod.Enabled", "false" });
            await ServiceResultShouldBeCached();
        }

        [Test]
        public async Task CachingRefreshTimeByConfiguration()
        {
            TimeSpan expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new [] { "RefreshTime", expectedRefreshTime.ToString()});
            await ServiceResultShouldRefreshOnBackgroundAfter(expectedRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByMethodConfiguration()
        {
            TimeSpan expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] { "Methods.CallService.RefreshTime", expectedRefreshTime.ToString() });
            await ServiceResultShouldRefreshOnBackgroundAfter(expectedRefreshTime);
        }

        private async Task SetCachingPolicyConfig(params string[][] keyValues)
        {
            _configDic.Clear();
            foreach (var keyValue in keyValues)
            {
                var key = keyValue[0];
                var value = keyValue[1];
                if (key != null && value != null)
                    _configDic[$"Discovery.Services.CachingTestService.CachingPolicy.{key}"] = value;
            }
            _kernel.RaiseConfigChangeEvent();
            await Task.Delay(200);
        }

        private async Task ClearCahingPolicyConfig()
        {
            await SetCachingPolicyConfig();
        }

        private async Task ServiceResultShouldBeCached()
        {
            await ResultShouldBe(FirstResult);
            _serviceResult = SecondResult;
            await ResultShouldBe(FirstResult, "Result should have been cached");
        }

        private async Task ServiceResultShouldNotBeCached()
        {
            await ResultShouldBe(FirstResult);
            _serviceResult = SecondResult;
            await ResultShouldBe(SecondResult, "Result shouldn't have been cached");
        }

        private async Task ServiceResultShouldRefreshOnBackgroundAfter(TimeSpan timeSpan)
        {
            await ResultShouldBe(FirstResult);
            _serviceResult = SecondResult;
            _now += timeSpan;
            await TriggerCacheRefreshOnBackground();   
            await ResultShouldBe(SecondResult, $"Cached value should have been background-refreshed after {timeSpan}");
        }

        private async Task TriggerCacheRefreshOnBackground()
        {
            await _proxy.CallService();
        }

        private async Task ResultShouldBe(string expectedResult, string message = null)
        {
            var result = await _proxy.CallService();
            result.ShouldBe(expectedResult, message);
        }
    }

    [HttpService(1234, Name="CachingTestService")]
    public interface ICachingTestService
    {
        [Cached]
        Task<string> CallService();

        [Cached]
        Task<string> OtherMethod();
    }
}
