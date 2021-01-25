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
    public class RecentlyRevokesCache : IRecentlyRevokesCache, IDisposable
    {
        private ILog                                                 Log { get; }
        public  MetricsContext                                       Metrics { get; }
        public  Func<CacheConfig>                                    GetRevokeConfig { get; }
        private ConcurrentDictionary<string, RevokeKeyItem>          RevokesIndex { get; }
        private ConcurrentQueue<(Task task, DateTime sendTime)>      OngoingTasks { get; }
        private ConcurrentQueue<(string key, DateTime receivedTime)> RevokesQueue { get; }
        private CancellationTokenSource                              ClearCancellationTokenSource { get; }

        public RecentlyRevokesCache(ILog log, MetricsContext metrics, Func<CacheConfig> getRevokeConfig)
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
            if (GetRevokeConfig().UseRecentlyRevokesCache)
            {
                if (task == null)
                    throw new ProgrammaticException("Received null task");

                OngoingTasks.Enqueue((task, sentTime));
            }
        }

        public void RegisterRevokeKey(string revokeKey, DateTime receivedRevokeTime)
        {
            if (GetRevokeConfig().UseRecentlyRevokesCache)
            {
                if (string.IsNullOrEmpty(revokeKey))
                    throw new ProgrammaticException("Received null or empty revokeKey");

                var revokeItem = RevokesIndex.GetOrAdd(revokeKey, s => new RevokeKeyItem());

                if (receivedRevokeTime > revokeItem.RevokeTime)
                {
                    lock (revokeItem.Locker)
                    {
                        if (receivedRevokeTime > revokeItem.RevokeTime)
                        {
                            revokeItem.RevokeTime = receivedRevokeTime;
                            RevokesQueue.Enqueue((revokeKey, receivedRevokeTime));
                        }
                    }
                }
            }
        }

        public DateTime? IsRecentlyRevoked(string revokeKey, DateTime compareTime)
        {
            if (GetRevokeConfig().UseRecentlyRevokesCache && 
                RevokesIndex.TryGetValue(revokeKey, out var revokeItem) && 
                revokeItem.RevokeTime > compareTime)
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
                    if (!GetRevokeConfig().UseRecentlyRevokesCache)
                        continue;

                    var oldestOutgoingTaskSendTime = DateTime.UtcNow; //default in case no ongoing tasks
                    while (OngoingTasks.TryPeek(out var taskTuple))
                    {
                        if (taskTuple.task.IsCompleted && OngoingTasks.TryDequeue(out taskTuple))
                            oldestOutgoingTaskSendTime = taskTuple.sendTime;
                        else
                            break;
                    }

                    while (RevokesQueue.TryPeek(out var revokeQueueItem))
                    {
                        if (revokeQueueItem.receivedTime < oldestOutgoingTaskSendTime)
                        {
                            if (RevokesIndex.TryGetValue(revokeQueueItem.key, out var revokeIndexItem))
                            {
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
                }
                catch (Exception e)
                {
                    Log.Error("Error removing items from cache", exception: e);
                }
                finally
                {
                    await Task.Delay(1000);
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

    public interface IRecentlyRevokesCache
    {
        void RegisterOutgoingRequest(Task task, DateTime sentTime);
        void RegisterRevokeKey(string revokeKey, DateTime receivedTime);
        DateTime? IsRecentlyRevoked(string revokeKey, DateTime compareTime);
    }

    public class RevokeKeyItem
    {
        public DateTime RevokeTime { get; set; }
        public object Locker { get; set; } = new object();
    }
}
