using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Utils;
using Gigya.ServiceContract.HttpService;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using Gigya.Common.Contracts.Attributes;

namespace Gigya.Microdot.UnitTests.Caching
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class CachingProxyTests
    {
        public const int AttRefreshTimeInMinutes = 2;

        const string FirstResult  = "First Result";
        const string SecondResult = "Second Result";

        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _kernel;
        private ICachingTestService _proxy;
        private ICachingTestService _serviceMock;
        private DateTime _now;
        private string _serviceResult;
        private ICacheRevoker _cacheRevoker;
        private Task _revokeDelay;
        private IRevokeListener _revokeListener;

        [OneTimeSetUp]
        public void OneTimeSetup()
        { 
            _configDic = new Dictionary<string,string>();
            _kernel = new TestingKernel<ConsoleLog>(mockConfig: _configDic);
            _kernel.Rebind(typeof(CachingProxyProvider<>))
                .ToSelf()      
                .InTransientScope();
            var fakeRevokingManager =new FakeRevokingManager();
            _kernel.Rebind<IRevokeListener>().ToConstant(fakeRevokingManager);
            _kernel.Rebind<ICacheRevoker>().ToConstant(fakeRevokingManager);

        }

        [SetUp]
        public void Setup()
        {            
            SetupServiceMock();
            SetupDateTime();
            _revokeDelay =Task.Delay(0);
            
            _proxy = _kernel.Get<ICachingTestService>();
            _cacheRevoker = _kernel.Get<ICacheRevoker>();
            _revokeListener = _kernel.Get<IRevokeListener>();
        }

        [TearDown]
        public void TearDown()
        {
            _kernel.Get<AsyncCache>().Clear();
        }

        private void SetupServiceMock()
        {             
            _serviceMock = Substitute.For<ICachingTestService>();
            _serviceMock.CallService().Returns(_ =>
            {
                return Task.FromResult(_serviceResult);
            });
            _serviceMock.CallServiceWithAttRefreshTime().Returns(_ =>
            {
                return Task.FromResult(_serviceResult);
            });
            _serviceMock.CallServiceWithoutAttRefreshTime().Returns(_ =>
            {
                return Task.FromResult(_serviceResult);
            });
            _serviceMock.CallRevocableService(Arg.Any<string>()).Returns(async s =>
            {
                var result = _serviceResult;
                await _revokeDelay;
                return new Revocable<string>
                {
                    Value = result,
                    RevokeKeys = new[] {s.Args()[0].ToString()}
                };
            });
        
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
            await ClearCachingPolicyConfig();
            await ServiceResultShouldBeCached();
        }

        [Test]
        public async Task CachingDisabledByConfiguration()
        {            
            await SetCachingPolicyConfig(new[] {"Enabled", "false"});
            await ServiceResultShouldNotBeCached();
        }

        [Test]
        public async Task CallServiceWithoutReturnValueSucceed() //bug #139872
        {
            await _proxy.CallServiceWithoutReturnValue();
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
        public async Task CallWhileRefreshShouldReturnOldValueAndAfterRefreshTheNewValue()
        {
            TimeSpan expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] { "RefreshTime", expectedRefreshTime.ToString() });

            var result = await _proxy.CallService();
            result.ShouldBe(FirstResult); 

            _now += expectedRefreshTime;
            _serviceResult = SecondResult;
            
            result = await _proxy.CallService(); //trigger a refresh, but until it will finish, return old result
            result.ShouldBe(FirstResult);

            await Task.Delay(100); //wait for refresh to end

            result = await _proxy.CallService();
            result.ShouldBe(SecondResult); //refreshed value should be returned
        }

        [Test]
        public async Task DoNotExtendExpirationWhenReadFromCache_CallAfterCacheItemIsExpiredShouldTriggerACallToTheService()
        {
            try
            {
                TimeSpan expectedExpirationTime = TimeSpan.FromSeconds(1);
                await SetCachingPolicyConfig(new[] { "ExpirationTime", expectedExpirationTime.ToString() },
                    new[] { "ExpirationBehavior", "DoNotExtendExpirationWhenReadFromCache" });

                //First call to service - value is cached
                var result = await _proxy.CallService();
                result.ShouldBe(FirstResult);

                _serviceResult = SecondResult;

                //No service call - cached value is used
                result = await _proxy.CallService();
                result.ShouldBe(FirstResult);

                //Wait for item to be expired
                await Task.Delay(1500);

                //Prev item is expired - make a call to the service
                result = await _proxy.CallService();
                result.ShouldBe(SecondResult);
            }
            catch (Exception e)
            {
                Assert.Inconclusive("Test sometimes fail in build server because of timing issues. Please run locally");
            }
        }

        [Test]
        public async Task ExtendExpirationWhenReadFromCache_CallAfterCacheItemIsExpiredAndExtendedShouldNotTriggerACallToTheService()
        {
            try
            {
                TimeSpan expectedExpirationTime = TimeSpan.FromSeconds(3);
                await SetCachingPolicyConfig(new[] { "ExpirationTime",     expectedExpirationTime.ToString() },
                                             new[] { "ExpirationBehavior", "ExtendExpirationWhenReadFromCache" });

                //First call to service - value is cached
                var result = await _proxy.CallService();
                result.ShouldBe(FirstResult);

                _serviceResult = SecondResult;

                //Time has passed, but expiration has not reached
                await Task.Delay(1000);

                //No service call - cached value is used and expiration is extended
                result = await _proxy.CallService();
                result.ShouldBe(FirstResult);

                //Additional time has passed (beyond the expectedExpirationTime)
                await Task.Delay(2100);

                //Prev item is not expired (expiration was extended) - no service call
                result = await _proxy.CallService();

                result.ShouldBe(FirstResult);
            }
            catch (ShouldAssertException e)
            {
                Assert.Inconclusive("Test sometimes fail in build server because of timing issues. Please run locally");
            }
        }

        #region Config overrides tests

        [Test]
        public async Task CachingRefreshTimeByDefault()
        {
            TimeSpan expectedRefreshTime = CachingPolicyConfig.Default.RefreshTime.Value;
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByServiceConfiguration()
        {
            TimeSpan expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] {"RefreshTime", expectedRefreshTime.ToString()});
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByMethodConfiguration()
        {
            TimeSpan expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithoutAttRefreshTime.RefreshTime", expectedRefreshTime.ToString()});
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByMethodAndServiceConfiguration_MethodConfigurationIsUsed()
        {
            TimeSpan methodRefreshTime = TimeSpan.FromSeconds(5);
            TimeSpan serviceRefreshTime = TimeSpan.FromSeconds(10);

            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithoutAttRefreshTime.RefreshTime", methodRefreshTime.ToString() },
                                         new[] { "RefreshTime", serviceRefreshTime.ToString() });
            await ServiceResultShouldRefreshAfter(methodRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByAttribute()
        {
            TimeSpan expectedRefreshTime = TimeSpan.FromMinutes(CachingProxyTests.AttRefreshTimeInMinutes);
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByMethodAndAttrConfiguration_MethodConfigurationIsUsed()
        {
            TimeSpan methodRefreshTime = TimeSpan.FromSeconds(5);

            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithAttRefreshTime.RefreshTime", methodRefreshTime.ToString() });

            await ServiceResultShouldRefreshAfter(methodRefreshTime, ServiceMethod.CallServiceWithAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByServiceAndAttrConfiguration_AttrConfigurationIsUsed()
        {
            TimeSpan serviceRefreshTime = TimeSpan.FromSeconds(10);

            await SetCachingPolicyConfig(new[] { "RefreshTime", serviceRefreshTime.ToString() });

            await ServiceResultShouldRefreshAfter(TimeSpan.FromMinutes(CachingProxyTests.AttRefreshTimeInMinutes), ServiceMethod.CallServiceWithAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeByMethodServiceAndAttrConfiguration_MethodConfigurationIsUsed()
        {
            TimeSpan methodRefreshTime = TimeSpan.FromSeconds(5);
            TimeSpan serviceRefreshTime = TimeSpan.FromSeconds(10);

            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithAttRefreshTime.RefreshTime", methodRefreshTime.ToString() },
                                         new[] { "RefreshTime", serviceRefreshTime.ToString() });

            await ServiceResultShouldRefreshAfter(methodRefreshTime, ServiceMethod.CallServiceWithAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromDefaultToService_ServiceConfigurationIsUsed()
        {
            //Trigger call
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithoutAttRefreshTime);

            //Adjust time to cause refresh
            _now += CachingPolicyConfig.Default.RefreshTime.Value;

            //Apply config change and test
            var expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] { "RefreshTime", expectedRefreshTime.ToString() });
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromServiceToDefault_DefaultConfigurationIsUsed()
        {
            //Trigger call
            var expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] { "RefreshTime", expectedRefreshTime.ToString() });
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithoutAttRefreshTime);

            //Adjust time to cause refresh
            _now += expectedRefreshTime;

            //Apply config change and test
            await SetCachingPolicyConfig();
            await ServiceResultShouldRefreshAfter(CachingPolicyConfig.Default.RefreshTime.Value, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromAttrToService_AttrConfigurationIsUsed()
        {
            //Trigger call
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithAttRefreshTime);

            //Adjust time to cause refresh
            var expectedRefreshTime = TimeSpan.FromMinutes(CachingProxyTests.AttRefreshTimeInMinutes);
            _now += expectedRefreshTime;

            //Apply config change and test
            await SetCachingPolicyConfig(new[] { "RefreshTime", TimeSpan.FromSeconds(10).ToString() });
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromDefaultToMethod_MethodConfigurationIsUsed()
        {
            //Trigger call
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithoutAttRefreshTime);

            //Adjust time to cause refresh
            _now += CachingPolicyConfig.Default.RefreshTime.Value;

            //Apply config change and test
            var expectedRefreshTime = TimeSpan.FromSeconds(20);
            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithoutAttRefreshTime.RefreshTime", expectedRefreshTime.ToString()});
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromMethodToDefault_DefaultConfigurationIsUsed()
        {
            //Trigger call
            var expectedRefreshTime = TimeSpan.FromSeconds(20);
            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithoutAttRefreshTime.RefreshTime", expectedRefreshTime.ToString() });
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithoutAttRefreshTime);

            //Adjust time to cause refresh
            _now += expectedRefreshTime;

            //Apply config change and test
            await SetCachingPolicyConfig();
            await ServiceResultShouldRefreshAfter(CachingPolicyConfig.Default.RefreshTime.Value, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromAttrToMethod_MethodConfigurationIsUsed()
        {
            //Trigger call
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithAttRefreshTime);

            //Adjust time to cause refresh
            _now += TimeSpan.FromMinutes(CachingProxyTests.AttRefreshTimeInMinutes);

            //Apply config change and test
            var expectedRefreshTime = TimeSpan.FromSeconds(20);
            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithAttRefreshTime.RefreshTime", expectedRefreshTime.ToString() });
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromServiceToMethod_MethodConfigurationIsUsed()
        {
            //Trigger call
            var expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] { "RefreshTime", expectedRefreshTime.ToString() });
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithoutAttRefreshTime);

            //Adjust time to cause refresh
            _now += expectedRefreshTime;

            //Apply config change and test
            expectedRefreshTime = TimeSpan.FromSeconds(30);
            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithoutAttRefreshTime.RefreshTime", expectedRefreshTime.ToString() });
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        [Test]
        public async Task CachingRefreshTimeConfigUpdateFromMethodToService_MethodConfigurationIsUsed()
        {
            //Trigger call
            var expectedRefreshTime = TimeSpan.FromSeconds(10);
            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithoutAttRefreshTime.RefreshTime", expectedRefreshTime.ToString() });
            await ResultShouldBe(FirstResult, serviceMethod: ServiceMethod.CallServiceWithoutAttRefreshTime);

            //Adjust time to cause refresh
            _now += expectedRefreshTime;

            //Apply config change and test
            await SetCachingPolicyConfig(new[] { "Methods.CallServiceWithoutAttRefreshTime.RefreshTime", expectedRefreshTime.ToString() },
                                         new[] { "RefreshTime", TimeSpan.FromSeconds(30).ToString() });
            await ServiceResultShouldRefreshAfter(expectedRefreshTime, ServiceMethod.CallServiceWithoutAttRefreshTime);
        }

        #endregion

        [Test]
        public async Task CachedDataShouldBeRevoked()
        {
            var key = Guid.NewGuid().ToString();
            await ClearCachingPolicyConfig();

            await ResultlRevocableServiceShouldBe(FirstResult, key);
            _serviceResult = SecondResult;
            await ResultlRevocableServiceShouldBe(FirstResult, key, "Result should have been cached");
            await _cacheRevoker.Revoke(key);
            _revokeListener.RevokeSource.WhenEventReceived(TimeSpan.FromMinutes(1));
            await Task.Delay(100);
            await ResultlRevocableServiceShouldBe(SecondResult, key, "Result shouldn't have been cached");
        }

        [Test]
        public async Task RevokeBeforeServiceResultReceivedShouldRevokeStaleValue()
        {
            var key = Guid.NewGuid().ToString();
            await ClearCachingPolicyConfig();
            var delay = new TaskCompletionSource<int>();
            _revokeDelay = delay.Task;
            var serviceCallWillCompleteOnlyAfterRevoke = ResultlRevocableServiceShouldBe(FirstResult, key, "Result should have been cached");
            await _cacheRevoker.Revoke(key);
            _revokeListener.RevokeSource.WhenEventReceived(TimeSpan.FromMinutes(1));
            delay.SetResult(1);
            await serviceCallWillCompleteOnlyAfterRevoke;
            await Task.Delay(100);
            _serviceResult = SecondResult;
            await ResultlRevocableServiceShouldBe(SecondResult, key, "Result shouldn't have been cached");
        }

        private async Task SetCachingPolicyConfig(params string[][] keyValues)
        {
            var changed = false;

            if (_configDic.Values.Count != 0 && keyValues.Length==0)
                changed = true;

            _configDic.Clear();
            foreach (var keyValue in keyValues)
            {
                var key = keyValue[0];
                var value = keyValue[1];
                if (key != null && value != null)
                {
                    _kernel.Get<OverridableConfigItems>()
                        .SetValue($"Discovery.Services.CachingTestService.CachingPolicy.{key}", value);
                    changed = true;
                }
            }
            if (changed)
            {
                await _kernel.Get<ManualConfigurationEvents>().ApplyChanges<DiscoveryConfig>();
                await Task.Delay(200);
            }
        }

        private async Task ClearCachingPolicyConfig()
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

        private async Task ServiceResultShouldRefreshAfter(TimeSpan timeSpan, ServiceMethod serviceMethod = ServiceMethod.CallService)
        {
            await ResultShouldBe(FirstResult, serviceMethod: serviceMethod);

            _serviceResult = SecondResult;

            //just before refresh time - cached result returned
            _now += timeSpan.Subtract(TimeSpan.FromSeconds(1));
            await ResultShouldBe(FirstResult, serviceMethod: serviceMethod);

            //just after refresh time - new result returned
            _now += TimeSpan.FromSeconds(1);
            await ResultShouldBe(SecondResult, serviceMethod: serviceMethod, message: $"Cached value should have been refreshed after {timeSpan}");
        }

        private async Task ResultShouldBe(string expectedResult, string message = null, ServiceMethod serviceMethod = ServiceMethod.CallService)
        {
            string result = null;

            switch (serviceMethod)
            {
                case ServiceMethod.CallService:
                    result = await _proxy.CallService();
                    break;
                case ServiceMethod.CallServiceWithAttRefreshTime:
                    result = await _proxy.CallServiceWithAttRefreshTime();
                    break;
                case ServiceMethod.CallServiceWithoutAttRefreshTime:
                    result = await _proxy.CallServiceWithoutAttRefreshTime();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(serviceMethod), serviceMethod, null);
            }

            result.ShouldBe(expectedResult, message);
        }

        private async Task ResultlRevocableServiceShouldBe(string expectedResult,string key ,string message = null)
        {
            var result = await _proxy.CallRevocableService(key);
            result.Value.ShouldBe(expectedResult, message);
        }
    }

    public enum ServiceMethod
    {
        CallService,
        CallServiceWithAttRefreshTime,
        CallServiceWithoutAttRefreshTime
    }

    [HttpService(1234)]
    public interface ICachingTestService
    {
        [Cached]
        Task<string> CallService();

        [Cached(RefreshTimeInMinutes = CachingProxyTests.AttRefreshTimeInMinutes,
                RefreshBehavior = RefreshBehavior.TryFetchNewValueOrUseOld)]
        Task<string> CallServiceWithAttRefreshTime();

        [Cached(RefreshBehavior = RefreshBehavior.TryFetchNewValueOrUseOld)]
        Task<string> CallServiceWithoutAttRefreshTime();

        [Cached]
        Task<string> OtherMethod();

        [Cached]
        Task<Revocable<string>> CallRevocableService(string keyToRevock);

        Task CallServiceWithoutReturnValue();
    }

    public class FakeRevokingManager : ICacheRevoker, IRevokeListener
    {
        private readonly BroadcastBlock<string> _broadcastBlock = new BroadcastBlock<string>(null);
        public Task Revoke(string key)
        {
            return _broadcastBlock.SendAsync(key);
        }

        public ISourceBlock<string> RevokeSource => _broadcastBlock;
    }

}
