using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Metrics;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public class RecentRevokesCache : IRecentRevokesCache, IDisposable
    {
        public int RevokesIndexCount => RevokesIndex.Count;
        public int RevokesQueueCount => RevokesQueue.Count;
        public int OngoingTasksCount => OngoingTasks.Count;

        private ILog                                                 Log { get; }
        private MetricsContext                                       Metrics { get; }
        private Func<CacheConfig>                                    GetRevokeConfig { get; }

        //In case solution gets too complicated, we can use Queue<RevokeKeyItem> as Value. Currently not used in order to reduce memory consumption 
        private ConcurrentDictionary<string, RevokeKeyItem>          RevokesIndex { get; }
        private ConcurrentQueue<(Task task, DateTime sendTime)>      OngoingTasks { get; }
        private ConcurrentQueue<(string key, DateTime receivedTime)> RevokesQueue { get; }
        private CancellationTokenSource                              ClearCancellationTokenSource { get; }

        public RecentRevokesCache(ILog log, MetricsContext metrics, Func<CacheConfig> getRevokeConfig)
        {
            Log = log;
            Metrics = metrics;
            GetRevokeConfig = getRevokeConfig;

            RevokesIndex  = new ConcurrentDictionary<string, RevokeKeyItem>();
            OngoingTasks  = new ConcurrentQueue<(Task task, DateTime sentTime)>();
            RevokesQueue  = new ConcurrentQueue<(string key, DateTime receivedTime)>();
            ClearCancellationTokenSource = new CancellationTokenSource();

            InitMetrics();

            Task.Factory.StartNew(CleanCache, ClearCancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void RegisterOutgoingRequest(Task task, DateTime sentTime)
        {
            if (GetRevokeConfig().DontCacheRecentlyRevokedResponses)
            {
                if (task == null)
                    throw new ProgrammaticException("Received null task");

                if (sentTime.Kind != DateTimeKind.Utc)
                    throw new ProgrammaticException("Received non UTC time");

                if (sentTime < DateTime.UtcNow.AddMinutes(-5)) 
                    throw new ProgrammaticException("Received out of range time");

                //a rc can be when entering items to the tasks queue, so oldest task time wont be first in queue
                //its ok as anyway they will be removed from the queue (after completion)
                OngoingTasks.Enqueue((task, sentTime));
            }
        }

        public void RegisterRevokeKey(string revokeKey, DateTime receivedRevokeTime)
        {
            if (GetRevokeConfig().DontCacheRecentlyRevokedResponses)
            {
                if (string.IsNullOrEmpty(revokeKey))
                    throw new ProgrammaticException("Received null or empty revokeKey");

                if (receivedRevokeTime.Kind != DateTimeKind.Utc)
                    throw new ProgrammaticException("Received non UTC time");

                if (receivedRevokeTime > DateTime.UtcNow.AddMinutes(5)) 
                    throw new ProgrammaticException("Received out of range time");

                var revokeItem = RevokesIndex.GetOrAdd(revokeKey, s => new RevokeKeyItem());

                if (receivedRevokeTime > revokeItem.RevokeTime)
                {
                    lock (revokeItem.Locker) //in case of rc we will always take the most recent receivedRevokeTime
                    {
                        if (receivedRevokeTime > revokeItem.RevokeTime)
                        {
                            revokeItem.RevokeTime = receivedRevokeTime;
                            RevokesQueue.Enqueue((revokeKey, receivedRevokeTime)); //can be a second item with the same revokeKey
                        }
                    }
                }
            }
        }

        public DateTime? TryGetRecentlyRevokedTime(string revokeKey, DateTime compareTime)
        {
            if (compareTime.Kind != DateTimeKind.Utc)
                throw new ProgrammaticException("Received non UTC time");

            if (GetRevokeConfig().DontCacheRecentlyRevokedResponses && 
                RevokesIndex.TryGetValue(revokeKey, out var revokeItem) && 
                revokeItem.RevokeTime >= compareTime)
                return revokeItem.RevokeTime;
            else
                return null;
        }

        public void Dispose()
        {
            ClearCancellationTokenSource?.Cancel();
            ClearCancellationTokenSource?.Dispose();
        }

        private async Task CleanCache()
        {
            while (!ClearCancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    //default in case no ongoing tasks, we take the time back to avoid rc in case an item was just added
                    var oldestOutgoingTaskSendTime = now.AddSeconds(-1); 

                    while (OngoingTasks.TryPeek(out var taskTuple))
                    {
                        if (taskTuple.task.IsCompleted || !GetRevokeConfig().DontCacheRecentlyRevokedResponses)
                            OngoingTasks.TryDequeue(out taskTuple);
                        else if (taskTuple.sendTime.AddMinutes(60) < now) //protection for too old non completed tasks (should not happen)
                        {
                            OngoingTasks.TryDequeue(out taskTuple);

                            Log.Critical(x => x("Remove non completed old task from queue", unencryptedTags: new
                            {
                                taskSendTime = taskTuple.sendTime,
                                cacheTime    = now
                            }));
                        }
                        else 
                        {
                            oldestOutgoingTaskSendTime = taskTuple.sendTime;
                            break;
                        }
                    }

                    while (RevokesQueue.TryPeek(out var revokeQueueItem) && revokeQueueItem.receivedTime < oldestOutgoingTaskSendTime)
                    {
                        if (RevokesIndex.TryGetValue(revokeQueueItem.key, out var revokeIndexItem))
                        {
                            //In case 'if' statement is false, we wont remove item from RevokesIndex, but there will be other RevokesQueue item that will remove it later
                            if (revokeIndexItem.RevokeTime < oldestOutgoingTaskSendTime)
                            {
                                lock (revokeIndexItem.Locker)
                                {
                                    if (revokeIndexItem.RevokeTime < oldestOutgoingTaskSendTime)
                                    {
                                        RevokesIndex.TryRemove(revokeQueueItem.key, out var removedItem);
                                    }
                                }
                            }
                        }

                        RevokesQueue.TryDequeue(out var dequeuedItem);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Error removing items from cache", exception: e);
                }
                finally
                {
                    await Task.Delay(GetRevokeConfig().DelayBetweenRecentlyRevokedCacheClean);
                }
            }
        }

        private void InitMetrics()
        {
            Metrics.Gauge("RevokesIndexEntries", () => RevokesIndex.Count, Unit.Items);
            Metrics.Gauge("RevokesQueueItems",   () => RevokesQueue.Count, Unit.Items);
            Metrics.Gauge("OngoingTasksItems",   () => OngoingTasks.Count, Unit.Items);
        }
    }

    public interface IRecentRevokesCache
    {
        void RegisterOutgoingRequest(Task task, DateTime sentTime);
        void RegisterRevokeKey(string revokeKey, DateTime receivedTime);
        DateTime? TryGetRecentlyRevokedTime(string revokeKey, DateTime compareTime);
        int RevokesIndexCount { get; }
        int RevokesQueueCount { get; }
        int OngoingTasksCount { get; }
    }

    public class RevokeKeyItem
    {
        public DateTime RevokeTime { get; set; }
        public object Locker { get; set; } = new object();
    }
}
