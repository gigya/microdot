using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.HttpService;
using Metrics;
using Metrics.MetricData;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

// ReSharper disable ConsiderUsingConfigureAwait (not relevant for tests)
namespace Gigya.Microdot.UnitTests.Caching
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class AsyncMemoizerRevokesTests
    {
        private const string cacheContextName = "AsyncCache";
        private const string memoizerContextName = "AsyncMemoizer";

        private DateTimeFake TimeFake { get; } = new DateTimeFake { UtcNow = DateTime.UtcNow };
        private IRecentlyRevokesCache RecentlyRevokesCache { get; set; }

        private AsyncCache CreateCache(ISourceBlock<string> revokeSource = null)
        {
            RecentlyRevokesCache = Substitute.For<IRecentlyRevokesCache>();
            RecentlyRevokesCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns((DateTime?) null);
            
            return new AsyncCache(new ConsoleLog(), Metric.Context(cacheContextName), TimeFake, new EmptyRevokeListener { RevokeSource = revokeSource }, ()=>new CacheConfig(), RecentlyRevokesCache);
        }

        private IMemoizer CreateMemoizer(AsyncCache cache)
        {
            var metadataProvider = new MetadataProvider();
            return new AsyncMemoizer(cache, metadataProvider, Metric.Context(memoizerContextName));
        }

        private CacheItemPolicyEx GetPolicy(double ttlSeconds = 60 * 60 * 6, double refreshTimeSeconds = 60) => new CacheItemPolicyEx
        {
            AbsoluteExpiration = DateTime.UtcNow + TimeSpan.FromSeconds(ttlSeconds),
            RefreshTime = TimeSpan.FromSeconds(refreshTimeSeconds),
            FailedRefreshDelay = TimeSpan.FromSeconds(1)
        };

        private MethodInfo ThingifyTaskRevokabkle { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTaskRevokable));

        private IThingFrobber CreateRevokableDataSource(string[] revokeKeys, params object[] results)
        {
            var revocableTaskResults = new List<Task<Revocable<Thing>>>();
            var dataSource = Substitute.For<IThingFrobber>();

            if (results == null)
                results = new object[] { null };

            foreach (var result in results)
            {
                if (result == null)
                    revocableTaskResults.Add(Task.FromResult(default(Revocable<Thing>)));
                else if (result is int)
                    revocableTaskResults.Add(Task.FromResult(new Revocable<Thing> { Value = new Thing { Id = ((int)result).ToString() }, RevokeKeys = revokeKeys }));
                else if (result is string)
                    revocableTaskResults.Add(Task.FromResult(new Revocable<Thing> { Value = new Thing { Id = ((string)result) }, RevokeKeys = revokeKeys }));
                else if (result is TaskCompletionSource<Revocable<Thing>>)
                    revocableTaskResults.Add(((TaskCompletionSource<Revocable<Thing>>)result).Task);
                else
                    throw new ArgumentException();
            }


            if (results.Any())
                dataSource.ThingifyTaskRevokable("someString").Returns(revocableTaskResults.First(), revocableTaskResults.Skip(1).ToArray());

            return dataSource;
        }

        [SetUp]
        public void SetUp()
        {         
            Metric.ShutdownContext(cacheContextName);
            TracingContext.ClearContext();
        }

        [Test]
        public async Task MemoizeAsync_RevokeBeforeRetrivalTaskCompletedCaused_NoIssues()
        {
            var completionSource = new TaskCompletionSource<Revocable<Thing>>();
            var dataSource = CreateRevokableDataSource(null, completionSource);
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);
            string firstValue = "first Value";
            //Call method to get results
            var resultTask = (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());

            //Post revoke message while results had not arrived
            revokesSource.PostMessageSynced("revokeKey");

            //Should have a single discarded revoke in meter
            GetMetricsData("Revoke").AssertEquals(new MetricsDataEquatable
            {
                MetersSettings = new MetricsCheckSetting { CheckValues = true },
                Meters = new List<MetricDataEquatable> {
                    new MetricDataEquatable {Name = "Discarded", Unit = Unit.Events, Value = 1},
                }
            });

            //Wait before sending results 
            await Task.Delay(100);
            completionSource.SetResult(new Revocable<Thing> { Value = new Thing { Id = firstValue }, RevokeKeys = new[] { "revokeKey" } });

            //Results should arive now
            var actual = await resultTask;
            dataSource.Received(1).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(firstValue);
            
            cache.CacheKeyCount.ShouldBe(1);
        }

        [Test]
        //Bug #134604
        public async Task MemoizeAsync_ExistingItemWasRefreshedByTtlAndReciviedRevoke_AfterRevokeCacheIsNotUsedAndItemIsFetchedFromDataSource()
        {
            var completionSource = new TaskCompletionSource<Revocable<Thing>>();
            completionSource.SetResult(new Revocable<Thing> { Value = new Thing { Id = "first Value" }, RevokeKeys = new[] { "revokeKey" } });
            var dataSource = CreateRevokableDataSource(null, completionSource);
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);
                                                                                                                              //To cause refresh in next call
            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy(refreshTimeSeconds:0));
            dataSource.Received(1).ThingifyTaskRevokable("someString"); //New item - fetch from datasource

            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            dataSource.Received(2).ThingifyTaskRevokable("someString"); //Refresh task - fetch from datasource

            //Post revoke message 
            revokesSource.PostMessageSynced("revokeKey");

            //We want to test that item was removed from cache after revoke and that new item is fetched from datasource
            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            dataSource.Received(3).ThingifyTaskRevokable("someString"); //Revoke received and item removed from cache - fetch from datasource 
        }

        [Test]
        public async Task MemoizeAsync_RevokableObjectShouldBeCachedAndRevoked()
        {
            string firstValue = "first Value";
            string secondValue = "second Value";
            var dataSource = CreateRevokableDataSource(new[] { "revokeKey" }, firstValue,secondValue);

            var revokesSource = new OneTimeSynchronousSourceBlock<string>();

            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(1).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(firstValue);

            //Read value from cache should be still 5
            actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(1).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(firstValue);
            //A single cache key should be stored in index
            cache.CacheKeyCount.ShouldBe(1);

            //No metric for Revoke
            GetMetricsData("Revoke").AssertEquals(new MetricsDataEquatable { Meters = new List<MetricDataEquatable> ()});

            //Post revoke message, no cache keys should be stored
            revokesSource.PostMessageSynced("revokeKey");
            cache.CacheKeyCount.ShouldBe(1);

            //Should have a single revoke in meter
            GetMetricsData("Revoke").AssertEquals(new MetricsDataEquatable
            {
                MetersSettings = new MetricsCheckSetting { CheckValues = true },
                Meters = new List<MetricDataEquatable> {
                    new MetricDataEquatable {Name = "Succeeded", Unit = Unit.Events, Value = 1},
                }
            });

            //Should not have an item removed in meter as in new behaviour we dont remove items from cache (only on expiry)
            GetMetricsData("Items").AssertEquals(new MetricsDataEquatable { Meters = new List<MetricDataEquatable>()});
            
            //Value should change to 6 
            actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(2).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(secondValue);
            cache.CacheKeyCount.ShouldBe(1);

            //Post revoke message to not existing key value still should be 6
            revokesSource.PostMessageSynced("NotExistin-RevokeKey");


            actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(2).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(secondValue);
            cache.CacheKeyCount.ShouldBe(1);
        }

        [Test]
        public async Task MemoizeAsync_AfterRefreshRevokeKeysAreAddedToReverseIndexCorrectly()
        {
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var dataSource = Substitute.For<IThingFrobber>();
            var result1  = new Revocable<Thing> { Value = new Thing { Id = "result1" }, RevokeKeys = new List<string>{"x"} };
            var result2  = new Revocable<Thing> { Value = new Thing { Id = "result2" }, RevokeKeys = new List<string>{"x", "y"} };
            dataSource.ThingifyTaskRevokable("someString").Returns(result1, result2);

            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy(refreshTimeSeconds: 0));//refresh in next call
            cache.RevokeKeysCount.ShouldBe(1);
            cache.CacheKeyCount.ShouldBe(1);

            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            cache.CacheKeyCount.ShouldBe(2);
            cache.RevokeKeysCount.ShouldBe(2);
        }

        [Test]
        public async Task MemoizeAsync_AfterRefreshRevokeKeysAreRemovedFromReverseIndexCorrectly()
        {
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var dataSource = Substitute.For<IThingFrobber>();
            var result1 = new Revocable<Thing> { Value = new Thing { Id = "result1" }, RevokeKeys = new List<string> { "x", "y" } };
            var result2 = new Revocable<Thing> { Value = new Thing { Id = "result2" }, RevokeKeys = new List<string> { "x" } };
            dataSource.ThingifyTaskRevokable("someString").Returns(result1, result2);

            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy(refreshTimeSeconds: 0));//refresh in next call
            cache.RevokeKeysCount.ShouldBe(2);
            cache.CacheKeyCount.ShouldBe(2);

            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            cache.CacheKeyCount.ShouldBe(1);
            cache.RevokeKeysCount.ShouldBe(1);
        }

        [Test]
        public async Task MemoizeAsync_AfterRefreshRevokeKeysArePreservedInReverseIndexCorrectly()
        {
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var dataSource = Substitute.For<IThingFrobber>();
            var result1 = new Revocable<Thing> { Value = new Thing { Id = "result1" }, RevokeKeys = new List<string> { "x", "y" } };
            var result2 = new Revocable<Thing> { Value = new Thing { Id = "result2" }, RevokeKeys = new List<string> { "x", "y" } };
            dataSource.ThingifyTaskRevokable("someString").Returns(result1, result2);

            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy(refreshTimeSeconds: 0));//refresh in next call
            cache.RevokeKeysCount.ShouldBe(2);
            cache.CacheKeyCount.ShouldBe(2);

            await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            cache.CacheKeyCount.ShouldBe(2);
            cache.RevokeKeysCount.ShouldBe(2);
        }

        [Test]
        public async Task MemoizeAsync_NotCachedCallWithoutRevokeShouldCacheDataSourceValue()
        {
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var dataSourceDelay = 1;
            var dataSourceResult1 = new Revocable<Thing> { Value = new Thing { Id = "result1" }, RevokeKeys = new List<string> { "x" } };
            var dataSource = new ThingFrobber(dataSourceDelay, new List<Revocable<Thing>> { dataSourceResult1 });

            RecentlyRevokesCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns((DateTime?)null);

            var result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult1.Value.Id); //call data source and get first value

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult1.Value.Id); //get cached value
        }

        [Test]
        public async Task MemoizeAsync_NotCachedCallWithIntefiringRevokeShouldNotCacheStaleValue()
        {
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var dataSourceDelay = 1;
            var dataSourceResult1 = new Revocable<Thing> { Value = new Thing { Id = "result1" }, RevokeKeys = new List<string> { "x" } };
            var dataSourceResult2 = new Revocable<Thing> { Value = new Thing { Id = "result2" }, RevokeKeys = new List<string> { "x" } };
            var dataSource = new ThingFrobber(dataSourceDelay, new List<Revocable<Thing>> { dataSourceResult1, dataSourceResult2 });

            //first call to data source will receive a revoke!!!
            RecentlyRevokesCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns(TimeFake.UtcNow, (DateTime?)null);

            var result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult1.Value.Id); //call data source and get first value 

            //revoke received while in first call to data source, so value is not cached

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult2.Value.Id); //call trigger a refresh because value is stale and a new data source value is returned

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult2.Value.Id); //cached value is returned
        }

        [Test]
        public async Task MemoizeAsync_RefreshWithoutRevokeShouldCacheNewValue()
        {
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var dataSourceDelay = 1;
            var dataSourceResult1 = new Revocable<Thing> { Value = new Thing { Id = "result1" }, RevokeKeys = new List<string> { "x" } };
            var dataSourceResult2 = new Revocable<Thing> { Value = new Thing { Id = "result2" }, RevokeKeys = new List<string> { "x" } };
            var dataSource = new ThingFrobber(dataSourceDelay, new List<Revocable<Thing>>{ dataSourceResult1, dataSourceResult2});

            RecentlyRevokesCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns((DateTime?)null);

            var result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy(refreshTimeSeconds: 0));//refresh in next call
            result.Value.Id.ShouldBe(dataSourceResult1.Value.Id); //call data source and get first value

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult1.Value.Id); //refresh triggered in the backround, but cached (old) value is returned

            await Task.Delay(dataSourceDelay + 100); //wait for refresh to finish

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult2.Value.Id); //second data source value returned (refreshed)
        }

        [Test]
        [NonParallelizable]
        public async Task MemoizeAsync_RefreshWithIntefiringRevokeShouldNotCacheStaleValue()
        {
            var cache = CreateCache(new OneTimeSynchronousSourceBlock<string>());
            var memoizer = CreateMemoizer(cache);

            var dataSourceDelay = 1;
            var dataSourceResult1 = new Revocable<Thing> { Value = new Thing { Id = "result1" }, RevokeKeys = new List<string> { "x" } };
            var dataSourceResult2 = new Revocable<Thing> { Value = new Thing { Id = "result2" }, RevokeKeys = new List<string> { "x" } };
            var dataSourceResult3 = new Revocable<Thing> { Value = new Thing { Id = "result3" }, RevokeKeys = new List<string> { "x" } };
            var dataSource = new ThingFrobber(dataSourceDelay, new List<Revocable<Thing>> { dataSourceResult1, dataSourceResult2, dataSourceResult3 });

            //second call to data source will receive a revoke!!!
            RecentlyRevokesCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns((DateTime?)null, TimeFake.UtcNow, (DateTime?)null);

            var result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy(refreshTimeSeconds: 0));//refresh in next call
            result.Value.Id.ShouldBe(dataSourceResult1.Value.Id); //call data source and get first value

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult1.Value.Id); //refresh triggered in the backround, but cached (old) value is returned

            //the backround refresh receives revoke while in call to remote service

            await Task.Delay(dataSourceDelay + 100); //wait for refresh to finish

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult3.Value.Id); //another refresh is triggered and its value is returned

            result = await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
            result.Value.Id.ShouldBe(dataSourceResult3.Value.Id); //latest refresh value was cached and returned
        }

        private async Task<Revocable<Thing>> CallWithMemoize(IMemoizer memoizer, IThingFrobber dataSource)
        {
            return await (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());
        }

        private static MetricsData GetMetricsData(string subContext)
        {
            return
                Metric.Context(cacheContextName)
                      .Context(subContext)
                      .DataProvider.CurrentMetricsData;
        }

        private static MetricsDataEquatable DefaultExpected()
        {
            return new MetricsDataEquatable
            {
                Counters = new List<MetricDataEquatable>(),
                Timers = new List<MetricDataEquatable> {
                    new MetricDataEquatable{Name = "Serialization",Unit= Unit.Calls},
                    new MetricDataEquatable{Name = "Roundtrip",Unit= Unit.Calls}
                }
            };
        }

    }
}
;