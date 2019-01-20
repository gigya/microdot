using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic.Collections;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Utils;
using Gigya.ServiceContract.HttpService;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Caching
{
    [TestFixture]
    public class CachingProxyTestsRevokeRace
    {
        private const string FirstResult  = "First Result";
        private const string SecondResult = "Second Result";

        private Dictionary<string, string> _configDic;
        private TestingKernel<ConsoleLog> _kernel;

        // We have to use actual DateTime and not a mock returning a constant/frozen value
        private Func<bool> _isTrueTime = () => true;
        private DateTime _now;

        private string _serviceResult;
        private ManualResetEvent _revokeSent = new ManualResetEvent(true);
        private ManualResetEvent _inMiddleOf = new ManualResetEvent(true);

        private ICachingTestService _proxy;
        private ICacheRevoker _cacheRevoker;
        private IRevokeListener _revokeListener;
        private ICachingTestService _serviceMock;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // State making issues with reconfiguration.
        }

        [SetUp]
        public void Setup()
        {
            // State making issues with reconfiguration.
        }

        [TearDown]
        public void TearDown()
        {
            var asyncCache = _kernel.TryGet<AsyncCache>();
            asyncCache?.Clear();
            asyncCache?.Dispose();
        }

        private void SetupServiceMock()
        {
            _serviceMock = Substitute.For<ICachingTestService>();
            _serviceMock.CallService().Returns(_ => Task.FromResult(_serviceResult));
            _serviceMock.CallRevocableService(Arg.Any<string>()).Returns(async s =>
            {
                var result = _serviceResult;

                // Signal we in the middle of function
                _inMiddleOf.Set();

                // Race condition "point" between Revoke and AddGet (caching of value)
                // It will await for revoke request in progress
                _revokeSent.WaitOne();

                await Task.Delay(100);

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


        [Description("The test demonstrating the race problem while refreshing the value.")]
        [Test]
        public async Task RevokesAhead_RaceOnRefresh()
        {
            var refreshTime = TimeSpan.FromSeconds(1);
            _configDic = new Dictionary<string,string>
            {
                ["Discovery.Services.CachingTestService.CachingPolicy.RefreshTime"]=refreshTime.ToString(),
                ["Discovery.Services.CachingTestService.CachingPolicy.Methods.CallRevocableService.RefreshTime"]=refreshTime.ToString()
            };
            _kernel = new TestingKernel<ConsoleLog>(mockConfig: _configDic);

            _kernel.Rebind(typeof(CachingProxyProvider<>)).ToSelf().InTransientScope();
            _kernel.Rebind<ICacheRevoker, IRevokeListener>().ToConstant(new FakeRevokingManager());
            
            var c = _kernel.Get<Func<DiscoveryConfig>>()(); // required

            c.Services["CachingTestService"].CachingPolicy.RefreshTime.ShouldBe(refreshTime);
            c.Services["CachingTestService"].CachingPolicy.Methods["CallRevocableService"].RefreshTime.ShouldBe(refreshTime);

            SetupServiceMock();
            SetupDateTime();

            _isTrueTime = () => true;

            _proxy = _kernel.Get<ICachingTestService>();
            _cacheRevoker = _kernel.Get<ICacheRevoker>();
            _revokeListener = _kernel.Get<IRevokeListener>();

            var key = Guid.NewGuid().ToString();

            _serviceResult = FirstResult;
            
            await ResultRevocableShouldBe(FirstResult, key);
            
            // still not updated
            _serviceResult = SecondResult;
            await ResultRevocableShouldBe(FirstResult, key);

            //
            await Task.Delay(refreshTime + TimeSpan.FromMilliseconds(100));

            // Simulate race between revoke and AddGet
            _revokeSent.Reset();
            _inMiddleOf.Reset();

            
            Task.WaitAll(

                Task.Run(async () =>
                {
                    // expected to trigger refresh task and signal to _inMiddleOf.
                    _serviceResult = "third";
                    var result = await _proxy.CallRevocableService(key);
                }),

                // Revoke the key (not truly, as value is not actually cached, yet).
                Task.Run(async() =>
                {
                    _inMiddleOf.WaitOne();
                        var eventWaiter = _revokeListener.RevokeSource.WhenEventReceived(TimeSpan.FromMinutes(1));
                        await _cacheRevoker.Revoke(key);
                        await eventWaiter;     // Wait the revoke will be processed
                    _revokeSent.Set();     // Signal to continue adding/getting
                })
            );

            _serviceResult = "Yellow";
            await ResultRevocableShouldBe("Yellow", key, "Result shouldn't have been cached");

        }

        /*
                       
                               AddOrGet
                           (service called)

                                  |
                                  |                                 Task.Run()
           CallRevocableService   |                                    |
         +----------------------> 1)                                   |
                                  |                                    |                                
                                  |_inMiddleOf.Set();                  |                                     
                                  2)----------------------------------->                                
                                  |                                    |                                
                                  |                           _inMiddleOf.WaitOne();                    
                                  |                                    |   _cacheRevoker.Revoke(key);  |
                                  |                                   3)------------------------------->
                                  |                                    |                         4)OnRevoke()
                                  |                            5)await eventWaiter;              8.1)  |--------> Enqueue
                                  |                                    |                               |        Maintainer
                         _revokeSent.WaitOne();                 6)revokeSent.Set()                     |
                                  |                                    |                               |
                                  7)<----------------------------------|                               |
                                  |                                    |                               |
                        8) AlreadyRevoked(...)                         |                               |
                                  |                                    |                               |
        <-------------------------+

        */

        [Test]
        [Repeat(1)]
        public async Task RevokeBeforeServiceResultReceived_ShouldRevokeStaleValue()
        {
            _configDic = new Dictionary<string,string>();
            _kernel = new TestingKernel<ConsoleLog>(mockConfig: _configDic);

            _kernel.Rebind(typeof(CachingProxyProvider<>)).ToSelf().InTransientScope();
            _kernel.Rebind<ICacheRevoker, IRevokeListener>().ToConstant(new FakeRevokingManager());
            var c = _kernel.Get<Func<DiscoveryConfig>>()(); // required

            SetupServiceMock();
            SetupDateTime();

            _proxy = _kernel.Get<ICachingTestService>();
            _cacheRevoker = _kernel.Get<ICacheRevoker>();
            _revokeListener = _kernel.Get<IRevokeListener>();


            var key = Guid.NewGuid().ToString();
            await ClearCachingPolicyConfig();

            // Init return value explicitly
            _serviceResult = FirstResult;

            // Simulate race between revoke and AddGet
            _revokeSent = new ManualResetEvent(false);
            _inMiddleOf = new ManualResetEvent(false);

            Task.WaitAll(

                // Call to service to cache FirstResult (and stuck until _revokeDelay signaled)
                Task.Run(async () =>
                {
                    var result = await _proxy.CallRevocableService(key);
                    result.Value.ShouldBe(FirstResult, "Result should have been cached");
                }),

                // Revoke the key (not truly, as value is not actually cached, yet).
                Task.Run(async() =>
                {
                    _inMiddleOf.WaitOne();
                        var eventWaiter = _revokeListener.RevokeSource.WhenEventReceived(TimeSpan.FromMinutes(1));
                        await _cacheRevoker.Revoke(key);
                        await eventWaiter;     // Wait the revoke will be processed
                    _revokeSent.Set();     // Signal to continue adding/getting
                })
            );

            // Init return value and expect to be returned, if not cached the first one!
            _serviceResult = SecondResult;
            await ResultRevocableShouldBe(SecondResult, key, "Result shouldn't have been cached");
        }

        [Test]
        public async Task RevokeMaintainer_ShouldCleanupOnlyOlderThan()
        {
            // Tear down using kernel, despite test doesn't
            _configDic = new Dictionary<string,string>();
            _kernel = new TestingKernel<ConsoleLog>(mockConfig: _configDic);

            var dateTime = DateTime.UtcNow;
            var total = 500;
            var maintainer = new TimeBoundConcurrentQueue<string>();

            // Add items, half older, half younger, IT IS A fifo QUEUE!
            for (int i = 0; i < total/2; i++)
                maintainer.Enqueue(dateTime - TimeSpan.FromHours(1), "revokeKey");   // older

            for (int i = 0; i < total/2; i++)
                maintainer.Enqueue(dateTime + TimeSpan.FromHours(1), "revokeKey");   // younger

            // expect to dequeue half
            var keys = maintainer.Dequeue(dateTime - TimeSpan.FromSeconds(30));

            maintainer.Count.ShouldBe(total / 2);
            keys.Count.ShouldBe(total / 2);
        }

        private void SetupDateTime()
        {
            _now = DateTime.UtcNow;
            var dateTimeMock = Substitute.For<IDateTime>();
            dateTimeMock.UtcNow.Returns(_=> _isTrueTime() ? DateTime.UtcNow : _now);
            _kernel.Rebind<IDateTime>().ToConstant(dateTimeMock);
        }

 
        private async Task SetCachingPolicyConfig(params string[][] keyValues)
        {
            bool changed = _configDic.Values.Count != 0 && keyValues.Length == 0;

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

        private async Task ResultRevocableShouldBe(string expectedResult, string key, string message = null)
        {
            var result = await _proxy.CallRevocableService(key);
            result.Value.ShouldBe(expectedResult, message);
        }
    }
}
