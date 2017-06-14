using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Gigya.ServiceContract.HttpService;
using Metrics;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

// ReSharper disable ConsiderUsingConfigureAwait (not relevant for tests)
namespace Gigya.Microdot.UnitTests.Caching
{
    // Calls to NSubstitute's .Received() method on async methods generate this warning.
    #pragma warning disable 4014 "Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call."

    [TestFixture]
    public class AsyncMemoizerTests
    {
        private MethodInfo ThingifyInt { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyInt));
        private MethodInfo ThingifyThing { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyThing));
        private MethodInfo ThingifyTask { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTask));
        private MethodInfo ThingifyTaskThing { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTaskThing));

        private MethodInfo ThingifyTaskRevokabkle { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTaskRevokabkle));

        private MethodInfo ThingifyTaskInt { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTaskInt));
        private MethodInfo ThingifyVoidMethod { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyVoid));
        private IThingFrobber EmptyThingFrobber { get; } = Substitute.For<IThingFrobber>();

        private CacheItemPolicyEx GetPolicy(double ttlSeconds = 60 * 60 * 6, double refreshTimeSeconds = 60) => new CacheItemPolicyEx
        {
            AbsoluteExpiration = DateTime.UtcNow + TimeSpan.FromSeconds(ttlSeconds),
            RefreshTime = TimeSpan.FromSeconds(refreshTimeSeconds),
            FailedRefreshDelay = TimeSpan.FromSeconds(1)
        };

        private DateTimeFake TimeFake { get; set; }

        private IMemoizer CreateMemoizer()
        {
            var metadataProvider = new MetadataProvider();
            TimeFake = new DateTimeFake { UtcNow = DateTime.UtcNow };
            return new AsyncMemoizer(new AsyncCache(new ConsoleLog(), Metric.Context("AsyncCache"), TimeFake), metadataProvider, Metric.Context("Tests"));
        }

        private IThingFrobber CreateRevokableDataSource(params object[] results)
        {
            var revocableTaskResults = new List<Task<IRevocable>>();
            var dataSource = Substitute.For<IThingFrobber>();

            if (results == null)
                results = new object[] { null };

            foreach (var result in results)
            {
                if (result == null)
                    revocableTaskResults.Add(Task.FromResult((IRevocable)default(Revocable<Thing>)));
                else if(result is int)
                    revocableTaskResults.Add(Task.FromResult((IRevocable)new Revocable<Thing>{ Value = new Thing {Id = (int)result}}));
                //else if (result is TaskCompletionSource<Revocable<Thing>>)
                  //  revocableTaskResults.Add(((TaskCompletionSource<Revocable<Thing>>)result).Task);                
                else
                    throw new ArgumentException();
            }
            

           if (results.Any())
              dataSource.ThingifyTaskRevokabkle("someString").Returns(revocableTaskResults.First(), revocableTaskResults.Skip(1).ToArray());

            return dataSource;
        }
        
        private IThingFrobber CreateDataSource(params object[] results)
        {
            var dataSource = Substitute.For<IThingFrobber>();
          
            var taskResults = new List<Task<Thing>>();

            if (results == null)
                results = new object[] { null };

            foreach (var result in results)
            {
                if (result == null)
                    taskResults.Add(Task.FromResult(default(Thing)));
                else if (result is int)
                    taskResults.Add(Task.FromResult(new Thing { Id = (int)result }));
                else if (result is TaskCompletionSource<Thing>)
                    taskResults.Add(((TaskCompletionSource<Thing>)result).Task);
                else
                    throw new ArgumentException();
            }

            if (results.Any())
                dataSource.ThingifyTaskThing("someString").Returns(taskResults.First(), taskResults.Skip(1).ToArray());
            
            var intResults = results.OfType<int>().ToArray();

            if (intResults.Any())
                dataSource.ThingifyTaskInt("someString").Returns(intResults.First(), intResults.Skip(1).ToArray());

            return dataSource;
        }
        
        [Test]
        public async Task MemoizeAsync_FirstCall_UsesDataSource_Revokable()
        {
            var revocable = new Revocable<int> {
                RevokeKeys = new List<string> {"test1", "test2"}
            };
            
            var dataSource = CreateRevokableDataSource(revocable);

            var actual = (Revocable<Thing>)await (Task<IRevocable>)CreateMemoizer().Memoize(dataSource, ThingifyTaskRevokabkle, new object[] { "someString" }, GetPolicy());

            actual.Value.Id.ShouldBe(5);
            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_FirstCall_UsesDataSource()
        {
            var dataSource = CreateDataSource(5);

            var actual = await (Task<Thing>)CreateMemoizer().Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy());
            
            actual.Id.ShouldBe(5);
            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        public void MemoizeAsync_CallFailsWhenNotAlreadyCached_Throws()
        {
            var failedTask = new TaskCompletionSource<Thing>();
            var dataSource = CreateDataSource(failedTask);

            failedTask.SetException(new Exception("Boo!!"));
            Should.Throw<Exception>(() => (Task)CreateMemoizer().Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy()));
        }

        [Test]
        public async Task MemoizeAsync_MultipleCalls_UsesDataSourceOnlyOnce()
        {
            var dataSource = CreateDataSource(5);
            var memoizer = CreateMemoizer();

            for (int i = 0; i < 100; i++)
            {
                var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy());
                actual.Id.ShouldBe(5);
            }

            dataSource.Received(1).ThingifyTaskThing("someString");
        }


        [Test]
        public async Task MemoizeAsync_MultipleCallsReturningNull_UsesDataSourceOnlyOnce()
        {
            var dataSource = CreateDataSource(null);
            var memoizer = CreateMemoizer();

            for (int i = 0; i < 100; i++)
            {
                var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy());
                actual.ShouldBeNull();
            }

            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_MultipleCallsPrimitive_UsesDataSourceOnlyOnce()
        {
            var dataSource = CreateDataSource(5);
            var memoizer = CreateMemoizer();

            for (int i = 0; i < 100; i++)
            {
                var actual = await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetPolicy());
                actual.ShouldBe(5);
            }

            dataSource.Received(1).ThingifyTaskInt("someString");
        }

        [Test]
        public async Task MemoizeAsync_CallsWithDifferentParams_UsesSeparateCacheSlots()
        {
            var dataSource = CreateDataSource(5);
            dataSource.ThingifyTaskThing("otherString").Returns(Task.FromResult(new Thing { Id = 7 }));
            IMemoizer memoizer = CreateMemoizer();

            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy())).Id.ShouldBe(5);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "otherString" }, GetPolicy())).Id.ShouldBe(7);

            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy())).Id.ShouldBe(5);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "otherString" }, GetPolicy())).Id.ShouldBe(7);

            dataSource.Received(1).ThingifyTaskThing("someString");
            dataSource.Received(1).ThingifyTaskThing("otherString");
        }

        [Test]
        public async Task MemoizeAsync_TwoCalls_RespectsCachingPolicy()
        {
            var dataSource = CreateDataSource(5, 7);
            IMemoizer memoizer = CreateMemoizer();

            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy(0.05))).Id.ShouldBe(5);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy())).Id.ShouldBe(5);
            await Task.Delay(100);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetPolicy())).Id.ShouldBe(7);

            dataSource.Received(2).ThingifyTaskThing("someString");
        }


        [Test]        
        public async Task MemoizeAsync_CallAfterRefreshTime_RefreshOnBackground()
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            var refreshTask = new TaskCompletionSource<Thing>();
            var args = new object[] { "someString" };
            var dataSource = CreateDataSource(5, refreshTask);

            IMemoizer memoizer = CreateMemoizer();
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5);

            // fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            // Refresh task hasn't finished, should get old value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5); // value is not refreshed yet. it is running on background
            
            // Complete refresh task and verify new value
            refreshTask.SetResult(new Thing { Id = 7, Name = "seven" });
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(7); // new value is expected now
        }

        [Test]
        public async Task MemoizeAsync_BackgroundRefreshFails_LastGoodValueStillReturned()
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            var refreshTask = new TaskCompletionSource<Thing>();
            var args = new object[] { "someString" };
            var dataSource = CreateDataSource(5, refreshTask);

            IMemoizer memoizer = CreateMemoizer();
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5);

            // fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            // Refresh task hasn't finished, should get old value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5); // value is not refreshed yet. it is running on background

            // Complete refresh task and verify new value
            refreshTask.SetException(new Exception("Boo!!"));
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5); // new value is expected now
        }

        [Test]
        public async Task MemoizeAsync_BackgroundRefreshFails_NextRequestAfterDelayTriggersRefresh()
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            TimeSpan failedRefreshDelay = TimeSpan.FromSeconds(1);
            var refreshTask = new TaskCompletionSource<Thing>();
            var args = new object[] { "someString" };
            var dataSource = CreateDataSource(5, refreshTask, 7);

            IMemoizer memoizer = CreateMemoizer();
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5);

            // fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            // Should trigger refresh task that won't be completed yet, should get old value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5);

            // Fail the first refresh task and verify old value (5) still returned.
            // FailedRefreshDelay hasn't passed so shouldn't trigger refresh.
            refreshTask.SetException(new Exception("Boo!!"));
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5);

            TimeFake.UtcNow += TimeSpan.FromMilliseconds(failedRefreshDelay.TotalMilliseconds * 0.7);

            // FailedRefreshDelay still hasn't passed so shouldn't trigger refresh.
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5);

            TimeFake.UtcNow += TimeSpan.FromMilliseconds(failedRefreshDelay.TotalMilliseconds * 0.7);

            // FailedRefreshDelay passed so should trigger refresh.
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(5);

            // Second refresh should succeed, should get new value (7);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy())).Id.ShouldBe(7);
        }


        [Test]
        public async Task MemoizeAsync_CallAfterRefreshTime_TTLNotExpired()
        {
            var dataSource = CreateDataSource(5, 7, 9);
            var args = new object[] { "someString" };

            IMemoizer memoizer = new AsyncMemoizer(new AsyncCache(new ConsoleLog(), Metric.Context("AsyncCache"), new DateTimeImpl()), new MetadataProvider(), Metric.Context("Tests"));

            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(5);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // Refresh just triggered, should get old value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(5); // value is not refreshed yet. it is running on background

            await Task.Delay(TimeSpan.FromSeconds(1));

            // Complete refresh task and verify new value
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(7); // value is not refreshed yet. it is running on background

            await Task.Delay(TimeSpan.FromSeconds(2));

            // We're past the original TTL, should still be the refreshed value (7), not another (9).
            // If 9 was returned, it means the data source was accessed again, probably because the TTL expired
            // (it shouldn't, every refresh should extend the TTL).
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(7); // new value is expected now
            dataSource.Received(2).ThingifyTaskThing(Arg.Any<string>());
        }


        [Test]
        public async Task MemoizeAsync_BackgroundRefreshFails_TTLNotExtended()
        {
            var args = new object[] { "someString" };
            var refreshTask = new TaskCompletionSource<Thing>();
            refreshTask.SetException(new Exception("Boo!!"));
            var dataSource = CreateDataSource(5, refreshTask, 7);

            IMemoizer memoizer = new AsyncMemoizer(new AsyncCache(new ConsoleLog(), Metric.Context("AsyncCache"), new DateTimeImpl()), new MetadataProvider(), Metric.Context("Tests"));

            // T = 0. No data in cache, should retrieve value from source (5).
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(5);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // T = 2. Past refresh time (1s), this triggers refresh in background, should get existing value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(5);

            await Task.Delay(TimeSpan.FromSeconds(1));

            // T = 3. Background refresh failed, but TTL (4s) not expired yet. Should still give old value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(5);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // T = 5. We're past the original TTL (4s), and refresh task failed. Items should have been evicted from cache by now.
            // New item (7) should come in.
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetPolicy(4, 1))).Id.ShouldBe(7);
            dataSource.Received(3).ThingifyTaskThing(Arg.Any<string>());
        }


        [Test]
        public async Task MemoizeAsync_MultipleIdenticalCallsBeforeFirstCompletes_TeamsWithFirstCall()
        {
            var dataSource = CreateDataSource();
            dataSource.ThingifyTaskInt("someString").Returns(async i => { await Task.Delay(100); return 1; }, async i => 2);
            IMemoizer memoizer = CreateMemoizer();

            var task1 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetPolicy());
            var task2 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetPolicy());
            var task3 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetPolicy());

            await Task.WhenAll(task1, task2, task3);

            dataSource.Received(1).ThingifyTaskInt("someString");
        }

        [Test]
        public void MemoizeAsync_MultipleIdenticalCallsBeforeFirstFails_TeamsWithFirstCall()
        {
            var dataSource = Substitute.For<IThingFrobber>();
            dataSource.ThingifyTaskInt("someString").Returns<Task<int>>(async i => { await Task.Delay(100); throw new InvalidOperationException(); }, async i => 2);
            IMemoizer memoizer = CreateMemoizer();

            var task1 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetPolicy());
            var task2 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetPolicy());
            var task3 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetPolicy());

            task1.ShouldThrow<InvalidOperationException>();
            task2.ShouldThrow<InvalidOperationException>();
            task3.ShouldThrow<InvalidOperationException>();

            dataSource.Received(1).ThingifyTaskInt("someString");
        }

        [Test]
        public void MemoizeAsync_NonCacheableMethods_Throws()
        {
            Should.Throw<ArgumentException>(() => CreateMemoizer().Memoize(EmptyThingFrobber, ThingifyInt, new object[] { "someString" }, GetPolicy()));
            Should.Throw<ArgumentException>(() => CreateMemoizer().Memoize(EmptyThingFrobber, ThingifyThing, new object[] { "someString" }, GetPolicy()));
            Should.Throw<ArgumentException>(() => CreateMemoizer().Memoize(EmptyThingFrobber, ThingifyTask, new object[] { "someString" }, GetPolicy()));
            Should.Throw<ArgumentException>(() => CreateMemoizer().Memoize(EmptyThingFrobber, ThingifyVoidMethod, new object[] { "someString" }, GetPolicy()));
        }
    }
}
;