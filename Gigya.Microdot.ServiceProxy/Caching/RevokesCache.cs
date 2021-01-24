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
    public class RevokesCache : IRevokesCache, IDisposable
    {
        private ILog                                                 Log { get; }
        public  MetricsContext                                       Metrics { get; }
        private ConcurrentDictionary<string, DateTime?>              RevokesIndex { get; }
        private ConcurrentQueue<(Task task, DateTime sendTime)>      OngoingTasks { get; }
        private ConcurrentQueue<(string key, DateTime receivedTime)> RevokesQueue { get; }
        private CancellationTokenSource                              ClearCancellationTokenSource { get; }

        public RevokesCache(ILog log, MetricsContext metrics)
        {
            Log = log;
            Metrics = metrics;

            RevokesIndex  = new ConcurrentDictionary<string, DateTime?>();
            OngoingTasks  = new ConcurrentQueue<(Task task, DateTime sentTime)>();
            RevokesQueue  = new ConcurrentQueue<(string key, DateTime receivedTime)>();
            ClearCancellationTokenSource = new CancellationTokenSource();

            InitMetrics();

            //Task.Factory.StartNew(CleanCache, ClearCancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void RegisterOutgoingRequest(Task task, DateTime sentTime)
        {
            if (task == null)
                throw new ProgrammaticException("Received null task");

            OngoingTasks.Enqueue((task, sentTime));
        }

        public void RegisterRevokeKey(string revokeKey, DateTime receivedTime)
        {
            var updatedTime = RevokesIndex.AddOrUpdate(revokeKey, receivedTime, (key, oldValue) =>
            {
                if (receivedTime > oldValue)
                {
                    RevokesQueue.Enqueue((revokeKey, receivedTime));
                    return receivedTime;
                }
                else
                    return oldValue;
            });
        }

        public DateTime? IsRecentlyRevoked(string revokeKey, DateTime compareTime)
        {
            if (RevokesIndex.TryGetValue(revokeKey, out var revokeTime) && revokeTime > compareTime)
                return revokeTime;
            else
                return null;
        }

        public void Dispose()
        {
            ClearCancellationTokenSource?.Cancel();
            ClearCancellationTokenSource?.Dispose();
        }

        //private async Task CleanCache()
        //{
        //    while (!ClearCancellationTokenSource.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            DateTime? oldestOutgoingTaskSendTime = null;
        //            while (OngoingTasks.TryPeek(out var taskTuple))
        //            {
        //                if (taskTuple.task.IsCompleted && OngoingTasks.TryDequeue(out taskTuple))
        //                    oldestOutgoingTaskSendTime = taskTuple.sendTime;
        //                else
        //                    break;
        //            }

        //            if (oldestOutgoingTaskSendTime != null)
        //            {
        //                while (RevokesQueue.TryPeek(out var revoke))
        //                {
        //                    if (revoke.receivedTime < oldestOutgoingTaskSendTime)
        //                    {
        //                        RevokesIndex.TryRemove(revoke.key, out var removedItem);
        //                        RevokesQueue.TryDequeue(out var dequeuedItem);
        //                    }
        //                }
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Error("Error removing items from cache", exception:e);
        //        }
        //        finally
        //        {
        //            await Task.Delay(1000);
        //        }
        //    }
        //}

        private void InitMetrics()
        {
            Metrics.Gauge("RevokesIndexEntries", () => RevokesIndex.Count, Unit.Items);
            Metrics.Gauge("RevokesQueueItems",   () => RevokesQueue.Count, Unit.Items);
            Metrics.Gauge("OngoingTasksItems",   () => OngoingTasks.Count, Unit.Items);
        }
    }

    public interface IRevokesCache
    {
        void RegisterOutgoingRequest(Task task, DateTime sentTime);
        void RegisterRevokeKey(string revokeKey, DateTime receivedTime);
        DateTime? IsRecentlyRevoked(string revokeKey, DateTime compareTime);
    }
}
