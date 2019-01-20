using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.ServiceContract.HttpService;
using Metrics;
using Metrics.MetricData;
using NSubstitute;

using NUnit.Framework;

using Shouldly;

// ReSharper disable ConsiderUsingConfigureAwait (not relevant for tests)
namespace Gigya.Microdot.UnitTests.Caching
{
    [TestFixture]
    public class AsyncMemoizerRevokesTests
    {
        private const string cacheContextName = "AsyncCache";
        private const string memoizerContextName = "AsyncMemoizer";

        private DateTimeFake TimeFake { get; } = new DateTimeFake { UtcNow = DateTime.UtcNow };

        private AsyncCache CreateCache(ISourceBlock<string> revokeSource = null)
        {

            var consoleLog = new ConsoleLog();
            return new AsyncCache(consoleLog, Metric.Context(cacheContextName), TimeFake, new EmptyRevokeListener { RevokeSource = revokeSource }, () => new CacheConfig());
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
                    revocableTaskResults.Add(Task.FromResult(new Revocable<Thing> { Value = new Thing { Id = (int)result }, RevokeKeys = revokeKeys }));
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
        }

        [Test]
        public async Task MemoizeAsync_RevokeBeforeRetrievalTaskCompletedCaused_NoIssues()
        {
            var completionSource = new TaskCompletionSource<Revocable<Thing>>();
            var dataSource = CreateRevokableDataSource(null, completionSource);
            var revokesSource = new OneTimeSynchronousSourceBlock<string>();
            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            // Call method to get results
            var resultTask = (Task<Revocable<Thing>>)memoizer.Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());

            // Post revoke message while results had not arrived
            revokesSource.PostMessageSynced("revokeKey");

            // Wait before sending results 
            await Task.Delay(100);
            completionSource.SetResult(new Revocable<Thing> { Value = new Thing { Id = 5 }, RevokeKeys = new[] { "revokeKey" } });

            // Results should arrive now
            var actual = await resultTask;
            dataSource.Received(1).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(5);
            
            cache.CacheKeyCount.ShouldBe(1);
        }



        [Test]
        public async Task MemoizeAsync_RevokableObjectShouldBeCachedAndRevoked()
        {

            var dataSource = CreateRevokableDataSource(new[] { "revokeKey" }, 5, 6);

            var revokesSource = new OneTimeSynchronousSourceBlock<string>();

            var cache = CreateCache(revokesSource);
            var memoizer = CreateMemoizer(cache);

            var actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(1).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(5);

            // Read value from cache should be still 5
            actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(1).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(5);
            // A single cache key should be stored in index
            cache.CacheKeyCount.ShouldBe(1);

            // No metric for Revoke
            GetMetricsData("Revoke").AssertEquals(new MetricsDataEquatable { Meters = new List<MetricDataEquatable> ()});

            // Post revoke message, no cache keys should be stored
            revokesSource.PostMessageSynced("revokeKey");
            cache.CacheKeyCount.ShouldBe(0);

            // Should have a single revoke in meter
            GetMetricsData("Revoke").AssertEquals(new MetricsDataEquatable
            {
                MetersSettings = new MetricsCheckSetting { CheckValues = true },
                Meters = new List<MetricDataEquatable> {
                    new MetricDataEquatable {Name = "Succeeded", Unit = Unit.Events, Value = 1},
                }
            });

            // Should have a single item removed in meter
            GetMetricsData("Items").AssertEquals(new MetricsDataEquatable
            {
                MetersSettings = new MetricsCheckSetting { CheckValues = true },
                Meters = new List<MetricDataEquatable> {
                    new MetricDataEquatable {Name = CacheEntryRemovedReason.Removed.ToString(), Unit = Unit.Items, Value = 1},
                }
            });


            // Value should change to 6 
            actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(2).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(6);
            cache.CacheKeyCount.ShouldBe(1);

            actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(2).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(6);
            cache.CacheKeyCount.ShouldBe(1);

            // Post revoke message to not existing key value still should be 6
            revokesSource.PostMessageSynced("NotExisting-RevokeKey");


            actual = await CallWithMemoize(memoizer, dataSource);
            dataSource.Received(2).ThingifyTaskRevokable("someString");
            actual.Value.Id.ShouldBe(6);
            cache.CacheKeyCount.ShouldBe(1);




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