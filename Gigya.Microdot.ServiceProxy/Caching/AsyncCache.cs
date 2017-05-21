#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Specialized;
using System.Runtime.Caching;
using System.Runtime.Caching.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Metrics;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public sealed class AsyncCache : IMemoryCacheManager, IServiceProvider, IDisposable
    {
        private IDateTime DateTime { get; }
        private ILog Log { get; }
        private MemoryCache MemoryCache { get; set; }
        private long LastCacheSizeBytes { get; set; }
        private MetricsContext Metrics { get; }

        private MetricsContext Hits { get; set; }
        private MetricsContext Misses { get; set; }
        private MetricsContext JoinedTeam { get; set; }
        private MetricsContext AwaitingResult { get; set; }
        private MetricsContext Failed { get; set; }
        private Counter ClearCache { get; set; }

        private const double MB = 1048576.0;
        private int _clearCount;



        public AsyncCache(ILog log, MetricsContext metrics, IDateTime dateTime)
        {
            DateTime = dateTime;
            Log = log;

            if (ObjectCache.Host == null)
                ObjectCache.Host = this;
            else
                Log.Error("AsyncCache: Failed to register with ObjectCache.Host since it was already registered. Cache memory size will not be reported to Metrics.NET. Please make sure AsyncCache is singleton to prevent this issue.");

            Clear();

            Metrics = metrics;
            InitMetrics();
        }


        private void InitMetrics()
        {
            // Gauges
            Metrics.Gauge("Size", () => LastCacheSizeBytes / MB, Unit.MegaBytes);
            Metrics.Gauge("Entries", () => MemoryCache.GetCount(), Unit.Items);
            Metrics.Gauge("SizeLimit", () => MemoryCache.CacheMemoryLimit / MB, Unit.MegaBytes);
            Metrics.Gauge("RamUsageLimit", () => MemoryCache.PhysicalMemoryLimit, Unit.Percent);

            // Counters
            AwaitingResult = Metrics.Context("AwaitingResult");

            // Meters
            Hits = Metrics.Context("Hits");
            Misses = Metrics.Context("Misses");
            JoinedTeam = Metrics.Context("JoinedTeam");
            Failed = Metrics.Context("Failed");
        }


        public Task GetOrAdd(string key, Func<Task> factory, Type taskResultType, CacheItemPolicyEx policy, params string[] metricsKeys)
        {
            var getValueTask = GetOrAdd(key, () => TaskConverter.ToWeaklyTypedTask(factory(), taskResultType), policy,  metricsKeys);
            return TaskConverter.ToStronglyTypedTask(getValueTask, taskResultType);
        }


        private Task<object> GetOrAdd(string key, Func<Task<object>> factory, CacheItemPolicyEx policy, string[] metricsKeys)
        {
            Func<bool, Task<object>> wrappedFactory = async removeOnException =>
            {
                try
                {
                    var result = await factory().ConfigureAwait(false);
                    AwaitingResult.Decrement(metricsKeys);
                    return result;
                }
                catch
                {
                    if (removeOnException)
                        MemoryCache.Remove(key); // Do not cache exceptions.

                    AwaitingResult.Decrement(metricsKeys);
                    Failed.Mark(metricsKeys);
                    throw;
                }
            };

            var newItem = new AsyncCacheItem();

            Task<object> resultTask;

            // Taking a lock on the newItem in case it actually becomes the item in the cache (if no item with that key
            // existed). For another thread, it will be returned into the existingItem variable and will block on the
            // second lock, preventing concurrent mutation of the same object.
            lock (newItem.Lock)
            {
                // Surprisingly, when using MemoryCache.AddOrGetExisting() where the item doesn't exist in the cache,
                // null is returned.
                var existingItem = (AsyncCacheItem)MemoryCache.AddOrGetExisting(key, newItem, policy);

                if (existingItem == null)
                {
                    Misses.Mark(metricsKeys);
                    AwaitingResult.Increment(metricsKeys);
                    newItem.CurrentValueTask = wrappedFactory(true);
                    newItem.NextRefreshTime = DateTime.UtcNow + policy.RefreshTime;
                    resultTask = newItem.CurrentValueTask;
                }
                else
                {
                    // This lock makes sure we're not mutating the same object as was added to the cache by an earlier
                    // thread (which was the first to add from 'newItem', for subsequent threads it will be 'existingItem').
                    lock (existingItem.Lock)
                    {
                        resultTask = existingItem.CurrentValueTask;

                        // Start refresh if an existing refresh ins't in progress and we've passed the next refresh time.
                        if (existingItem.RefreshTask == null && DateTime.UtcNow >= existingItem.NextRefreshTime)
                        {
                            existingItem.RefreshTask = ((Func<Task>)(async () =>
                                {
                                    try
                                    {
                                        var getNewValue = wrappedFactory(false);
                                        await getNewValue.ConfigureAwait(false);
                                        existingItem.CurrentValueTask = getNewValue;
                                        existingItem.NextRefreshTime = DateTime.UtcNow + policy.RefreshTime;
                                        existingItem.RefreshTask = null;
                                        MemoryCache.Set(new CacheItem(key, existingItem), policy);
                                    }
                                    catch
                                    {
                                        existingItem.NextRefreshTime = DateTime.UtcNow + policy.FailedRefreshDelay;
                                        existingItem.RefreshTask = null;
                                    }
                                })).Invoke();
                        }
                    }

                    if (resultTask.GetAwaiter().IsCompleted)
                         Hits.Mark(metricsKeys);
                    else
                        JoinedTeam.Mark(metricsKeys);
                }
            }

            return resultTask;
        }


        public void Clear()
        {
            var oldMemoryCache = MemoryCache;
            MemoryCache = new MemoryCache(nameof(AsyncCache) + Interlocked.Increment(ref _clearCount), new NameValueCollection { { "PollingInterval", "00:00:01" } });

            if (oldMemoryCache != null)
            {
                // Disposing of MemoryCache can be a CPU intensive task and should therefore not block the current thread.
                Task.Run(() => oldMemoryCache.Dispose());
                ClearCache.Increment();
            }
        }


        public void UpdateCacheSize(long size, MemoryCache cache)
        {
            if (cache != MemoryCache)
                return;

            LastCacheSizeBytes = size;
        }


        public void ReleaseCache(MemoryCache cache) { }


        public object GetService(Type serviceType)
        {
            return serviceType == typeof(IMemoryCacheManager) ? this : null;
        }


        public void Dispose()
        {
            MemoryCache?.Dispose();
        }
    }
}