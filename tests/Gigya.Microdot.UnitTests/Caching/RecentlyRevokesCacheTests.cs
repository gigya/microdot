using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceProxy.Caching;
using Metrics;
using NUnit.Framework;
using NSubstitute;

namespace Gigya.Microdot.UnitTests.Caching
{
    [TestFixture]
    public class RecentlyRevokesCacheTests
    {
        private IRecentRevokesCache RecentlyRevokesCache { get; set; }

        [SetUp]
        public void SetUp()
        {
            RecentlyRevokesCache = new RecentRevokesCache(Substitute.For<ILog>(),
                Metric.Context("RecentlyRevokesCache"), () => new CacheConfig
                {
                    DontCacheRecentlyRevokedResponses = true,
                    DelayBetweenRecentlyRevokedCacheClean = 100
                });
        }

        [Test]
        public async Task CleanCache_CacheContainsRevokeItemsAndCompletedTaskAndRevokeItemsTimeIsBeforeNow_RevokeItemsAndTaskAreCleaned()
        {
            var now = DateTime.UtcNow;

            RecentlyRevokesCache.RegisterOutgoingRequest(Task.CompletedTask, now);

            for (int i = 1; i <= 100; i++)
            {
                RecentlyRevokesCache.RegisterRevokeKey($"key_{i}", now.AddSeconds(-i));
            }

            //Wait for queue to clean up
            await Task.Delay(500);

            Assert.AreEqual(0, RecentlyRevokesCache.OngoingTasksCount);
            Assert.AreEqual(0, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(0, RecentlyRevokesCache.RevokesIndexCount);
        }

        [Test]
        public async Task CleanCache_CacheContainsRevokeItemsAndRunningTaskAndRevokeItemsTimeIsBeforeTaskTime_RevokeItemsAreCleanedAndTaskRemainsInQueue()
        {
            var now = DateTime.UtcNow;
            var tcs = new TaskCompletionSource<bool>();

            RecentlyRevokesCache.RegisterOutgoingRequest(tcs.Task, now);

            for (int i = 1; i <= 100; i++)
            {
                RecentlyRevokesCache.RegisterRevokeKey($"key_{i}", now.AddSeconds(-i));
            }

            //Wait for queue to clean up
            await Task.Delay(500);

            Assert.AreEqual(1, RecentlyRevokesCache.OngoingTasksCount);
            Assert.AreEqual(0, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(0, RecentlyRevokesCache.RevokesIndexCount);

            tcs.SetResult(true);
        }

        [Test]
        public async Task CleanCache_CacheContainsRevokeItemAndZeroTasksAndTimeOfItemIsBeforeNow_RevokeItemsAreCleaned()
        {
            var now = DateTime.UtcNow;

            //we put large value, because cache takes now back (second)
            RecentlyRevokesCache.RegisterRevokeKey($"key", now.AddMilliseconds(-2000));

            //Wait for queue to clean up
            await Task.Delay(500);

            Assert.AreEqual(0, RecentlyRevokesCache.OngoingTasksCount);
            Assert.AreEqual(0, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(0, RecentlyRevokesCache.RevokesIndexCount);
        }

        [Test]
        public async Task CleanCache_CacheContainsRevokeItemAndCompletedTaskAndTimeOfRevokeItemIsAfterNow_RevokeItemsAreNotCleanedAndTaskIsRemoved()
        {
            var now = DateTime.UtcNow;

            RecentlyRevokesCache.RegisterOutgoingRequest(Task.CompletedTask, now);
            RecentlyRevokesCache.RegisterRevokeKey($"key", now.AddMilliseconds(1000));

            //Wait for queue to clean up
            await Task.Delay(500);

            Assert.AreEqual(0, RecentlyRevokesCache.OngoingTasksCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);
        }

        [Test]
        public async Task CleanCache_CacheContainsRevokeItemAndRunningTaskAndTimeOfRevokeItemIsAfterTaskTime_RevokeItemsAndTaskAreNotCleaned()
        {
            var now = DateTime.UtcNow;
            var tcs = new TaskCompletionSource<bool>();

            RecentlyRevokesCache.RegisterOutgoingRequest(tcs.Task, now);
            RecentlyRevokesCache.RegisterRevokeKey($"key", now.AddMilliseconds(1000));

            //Wait for queue to clean up
            await Task.Delay(500);

            Assert.AreEqual(1, RecentlyRevokesCache.OngoingTasksCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);

            tcs.SetResult(true);
        }

        [Test]
        public async Task CleanCache_CacheContainsRevokeItemAndZeroTasksAndTimeOfItemIsAfterNow_RevokeItemsAreNotCleaned()
        {
            var now = DateTime.UtcNow;

            RecentlyRevokesCache.RegisterRevokeKey($"key", now.AddMilliseconds(1000));

            //Wait for queue to clean up
            await Task.Delay(500);

            Assert.AreEqual(0, RecentlyRevokesCache.OngoingTasksCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);
        }

        [Test]
        [TestCase(2)] //future
        [TestCase(-2)] //past
        public void TryGetRecentlyRevokedTime_RevokeKeyWasNotRegistered_ReturnsNull(int secondsShift)
        {
            Assert.IsNull(RecentlyRevokesCache.TryGetRecentlyRevokedTime("key", DateTime.UtcNow.AddSeconds(secondsShift)));
        }

        [Test]
        public void TryGetRecentlyRevokedTime_RevokeTimeIsBeforeOrEqualToCompareTime_ReturnsNull()
        {
            var now = DateTime.UtcNow;
            var tcs = new TaskCompletionSource<bool>();

            //so revokeKey wont be cleaned in the background
            RecentlyRevokesCache.RegisterOutgoingRequest(tcs.Task, now);

            var revokeTime = now.AddMilliseconds(100);
            var compareTime = revokeTime.AddMilliseconds(1);
            RecentlyRevokesCache.RegisterRevokeKey("key", revokeTime);

            Assert.IsNull(RecentlyRevokesCache.TryGetRecentlyRevokedTime("key", compareTime));

            tcs.SetResult(true);
        }

        [Test]
        [TestCase(0)] //revokeTime=compareTime
        [TestCase(-1)] //revokeTime<compareTime
        public void TryGetRecentlyRevokedTime_RevokeTimeIsAfterOrEqualCompareTime_ReturnsRevokeTime(int secondsShift)
        {
            var now = DateTime.UtcNow;
            var tcs = new TaskCompletionSource<bool>();

            //so revokeKey wont be cleaned in the background
            RecentlyRevokesCache.RegisterOutgoingRequest(tcs.Task, now);

            var revokeTime = now;
            var compareTime = revokeTime.AddSeconds(secondsShift);
            RecentlyRevokesCache.RegisterRevokeKey("key", revokeTime);

            Assert.AreEqual(revokeTime, RecentlyRevokesCache.TryGetRecentlyRevokedTime("key", compareTime).Value);

            tcs.SetResult(true);
        }

        [Test]
        public void RegisterOutgoingRequest_ItemEnqueued()
        {
            var tcs = new TaskCompletionSource<bool>();

            //so revokeKey wont be cleaned in the background
            RecentlyRevokesCache.RegisterOutgoingRequest(tcs.Task, DateTime.UtcNow);

            Assert.AreEqual(1, RecentlyRevokesCache.OngoingTasksCount);

            tcs.SetResult(true);
        }

        [Test]
        public void RegisterRevokeKey_KeyIsAddedToBothQueueAndIndex()
        {
            RecentlyRevokesCache.RegisterRevokeKey("key", DateTime.UtcNow.AddMinutes(5));

            Assert.AreEqual(1, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);
        }

        [Test]
        public void RegisterRevokeKey_OverrideAnExistingKeyWithAboveTime_RevokeItemIsUpdated()
        {
            RecentlyRevokesCache.RegisterRevokeKey("key", DateTime.UtcNow.AddMinutes(1));

            Assert.AreEqual(1, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);

            RecentlyRevokesCache.RegisterRevokeKey("key", DateTime.UtcNow.AddMinutes(2));

            Assert.AreEqual(2, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);
        }

        [Test]
        public void RegisterRevokeKey_OverrideAnExistingKeyWithBelowTime_RevokeItemIsNotUpdated()
        {
            RecentlyRevokesCache.RegisterRevokeKey("key", DateTime.UtcNow.AddMinutes(2));

            Assert.AreEqual(1, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);

            RecentlyRevokesCache.RegisterRevokeKey("key", DateTime.UtcNow.AddMinutes(1));

            Assert.AreEqual(1, RecentlyRevokesCache.RevokesQueueCount);
            Assert.AreEqual(1, RecentlyRevokesCache.RevokesIndexCount);
        }
    }
}
