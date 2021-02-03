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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.Caching.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.ServiceContract.HttpService;
using Metrics;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.ServiceProxy.Caching
{

    public sealed class AsyncCache : IMemoryCacheManager, IServiceProvider, IDisposable
    {
        private IDateTime DateTime { get; }
        private Func<CacheConfig> GetRevokeConfig { get; }
        private ILog Log { get; }
        private MemoryCache MemoryCache { get; set; }
        private long LastCacheSizeBytes { get; set; }
        private MetricsContext Metrics { get; }
        private IRecentlyRevokesCache RecentlyRevokeCache { get; set; }

        private ConcurrentDictionary<string, HashSet<string>> RevokeKeyToCacheKeysIndex { get; set; } = new ConcurrentDictionary<string, HashSet<string>>();

        private MetricsContext Hits { get; set; }
        private MetricsContext Misses { get; set; }
        private MetricsContext JoinedTeam { get; set; }
        private MetricsContext AwaitingResult { get; set; }
        private MetricsContext Failed { get; set; }
        private Counter ClearCache { get; set; }

        private MetricsContext Items { get; set; }
        private MetricsContext Revokes { get; set; }

        private IDisposable RevokeDisposable { get; }
        private const double MB = 1048576.0;
        private int _clearCount;

        public int RevokeKeysCount => RevokeKeyToCacheKeysIndex.Count;

        /// <summary>
        /// Not thread safe used for testing
        /// </summary>
        internal int CacheKeyCount => RevokeKeyToCacheKeysIndex.Sum(item => item.Value.Count);

        private static long oneTime = 0;

        public AsyncCache(ILog log, MetricsContext metrics, IDateTime dateTime, IRevokeListener revokeListener, 
                          Func<CacheConfig> getRevokeConfig, IRecentlyRevokesCache revokeCache)
        {
            DateTime = dateTime;
            GetRevokeConfig = getRevokeConfig;
            Log = log;
            RecentlyRevokeCache = revokeCache;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (ObjectCache.Host == null
                && Interlocked.CompareExchange(ref oneTime, 1, 0) == 0)
            {
                ObjectCache.Host = this;
            }
            else
                Log.Error("AsyncCache: Failed to register with ObjectCache.Host since it was already registered. Cache memory size will not be reported to Metrics.NET. Please make sure AsyncCache is singleton to prevent this issue.");

            Clear();

            Metrics = metrics;
            InitMetrics();
            var onRevoke = new ActionBlock<string>(OnRevoke);
            RevokeDisposable = revokeListener.RevokeSource.LinkTo(onRevoke);
        }

        private Task OnRevoke(string revokeKey)
        {
            if (string.IsNullOrEmpty(revokeKey))
            {
                Log.Warn("Error while revoking cache, revokeKey can't be null");
                return Task.FromResult(false);
            }

            RecentlyRevokeCache.RegisterRevokeKey(revokeKey, DateTime.UtcNow);

            var shouldLog = GetRevokeConfig().LogRevokes;

            try
            {    
                if (shouldLog)
                    Log.Info(x=>x("Revoke request received", unencryptedTags: new {revokeKey}));

                if (RevokeKeyToCacheKeysIndex.TryGetValue(revokeKey, out HashSet<string>  cacheKeys))
                {
                    string[] arrayOfCacheKeys = null;
					
                    lock (cacheKeys)
                    {						
                        arrayOfCacheKeys = cacheKeys.ToArray(); // To prevent iteration over modified collection.
                    }

                    //race condition situation may happen, in which a key was added (in other thread) to cacheKeys after ToArray,
                    //and revoke will not be applied. this rc WILL BE RESOLVED by the recently revoke mechanism

                    if (shouldLog && arrayOfCacheKeys.Length==0)
                       Log.Info(x => x("There is no CacheKey to Revoke", unencryptedTags: new { revokeKey }));

                    foreach (var cacheKey in arrayOfCacheKeys)
                    {
                        if (shouldLog)
                            Log.Info(x => x("Revoking cacheKey", unencryptedTags: new { revokeKey, cacheKey }));

                        var cacheItem = ((AsyncCacheItem) MemoryCache.Get(cacheKey));

                        if (cacheItem != null)
                        {
                            lock (cacheItem.Lock)
                            {
                                cacheItem.IsStale = true; //must not be inside cacheKeys lock (to prevent deadlock)
                            }
                        }

                    }
                    
                    Revokes.Meter("Succeeded", Unit.Events).Mark();
                }
                else
                {
                    if (shouldLog)
                        Log.Info(x => x("Key is not cached. No revoke is needed", unencryptedTags: new { revokeKey }));

                    Revokes.Meter("Discarded", Unit.Events).Mark();
                }                
            }
            catch (Exception ex)
            {
                Revokes.Meter("Failed", Unit.Events).Mark();
                Log.Warn("Error while revoking cache", exception: ex, unencryptedTags: new {revokeKey});
            }
            return Task.FromResult(true);
        }


        private void InitMetrics()
        {
            // Gauges
            Metrics.Gauge("Size", () => LastCacheSizeBytes / MB, Unit.MegaBytes);
            Metrics.Gauge("Entries", () => MemoryCache.GetCount(), Unit.Items);
            Metrics.Gauge("SizeLimit", () => MemoryCache.CacheMemoryLimit / MB, Unit.MegaBytes);
            Metrics.Gauge("RamUsageLimit", () => MemoryCache.PhysicalMemoryLimit, Unit.Percent);
            Metrics.Gauge("ReverseIndexEntries", () => RevokeKeyToCacheKeysIndex.Count, Unit.Items);

            // Counters
            AwaitingResult = Metrics.Context("AwaitingResult");
            ClearCache = Metrics.Counter("ClearCache", Unit.Calls);

            // Meters
            Hits = Metrics.Context("Hits");
            Misses = Metrics.Context("Misses");
            JoinedTeam = Metrics.Context("JoinedTeam");
            Failed = Metrics.Context("Failed");

            Items = Metrics.Context("Items");
            Revokes = Metrics.Context("Revoke");
        }


        public Task GetOrAdd(string key, Func<Task> factory, Type taskResultType, CacheItemPolicyEx policy, string groupName, string logData, string[] metricsKeys)
        {
            var getValueTask = GetOrAdd(key, () => TaskConverter.ToWeaklyTypedTask(factory(), taskResultType), policy, groupName, logData, metricsKeys, taskResultType);
            return TaskConverter.ToStronglyTypedTask(getValueTask, taskResultType);
        }

        private Task<object> GetOrAdd(string key, Func<Task<object>> factory, CacheItemPolicyEx policy, string groupName, string logData, string[] metricsKeys, Type taskResultType)
        {
            var shouldLog = ShouldLog(groupName);

            var newItem = shouldLog ? 
                new AsyncCacheItem {GroupName =  string.Intern(groupName), LogData = logData} : 
                new AsyncCacheItem (); // if log is not needed, then do not cache unnecessary details which will blow up the memory

            Task<object> resultTask;
            // Taking a lock on the newItem in case it actually becomes the item in the cache (if no item with that key
            // existed). For another thread, it will be returned into the existingItem variable and will block on the
            // second lock, preventing concurrent mutation of the same object.
            lock (newItem.Lock)
            {
                if (typeof(IRevocable).IsAssignableFrom(taskResultType))
                    policy.RemovedCallback += ItemRemovedCallback;
                
                // Surprisingly, when using MemoryCache.AddOrGetExisting() where the item doesn't exist in the cache,
                // null is returned.
                var existingItem = (AsyncCacheItem)MemoryCache.AddOrGetExisting(key, newItem, policy);
                
                if (existingItem == null)
                {
                    Misses.Mark(metricsKeys);
                    AwaitingResult.Increment(metricsKeys);
                    var requestTask = ExecuteRemoteRequest(key, factory, groupName, logData, metricsKeys, shouldLog, newItem);

                    newItem.NextRefreshTime = DateTime.UtcNow + policy.RefreshTime;
                    newItem.CurrentValueTask = requestTask.ContinueWith<object>(t =>
                    {
                        try
                        {
                            if (t.Result.isRecentlyRevoked)
                                lock (newItem.Lock)
                                    newItem.IsStale = true;

                            return t.Result.value;
                        }
                        catch (AggregateException e) 
                        {
                            throw e.InnerExceptions?.FirstOrDefault() ?? e;
                        }
                    });
                    
                    resultTask = newItem.CurrentValueTask;

                    if (shouldLog)
                        Log.Info(x => x("Item added to cache", unencryptedTags: new
                        {
                            cacheKey = key,
                            cacheGroup = groupName,
                            cacheData = logData
                        }));
                }
                else
                {
                    // This lock makes sure we're not mutating the same object as was added to the cache by an earlier
                    // thread (which was the first to add from 'newItem', for subsequent threads it will be 'existingItem').
                    lock (existingItem.Lock)
                    {
                        var cacheSuppress = TracingContext.CacheSuppress;
                        var shouldSuppressCache = cacheSuppress != null && 
                                                 (cacheSuppress == CacheSuppress.UpToNextServices ||
                                                  cacheSuppress == CacheSuppress.RecursiveAllDownstreamServices);

                        // Start refresh if an existing refresh isn't in progress and we've passed the next refresh time.
                        if (   (shouldSuppressCache || existingItem.IsStale || existingItem.CurrentValueTask.IsFaulted)
                            || (   (DateTime.UtcNow >= existingItem.NextRefreshTime) 
                                && (existingItem.RefreshTask == null || existingItem.RefreshTask.IsCompleted)))
                        {
                            existingItem.RefreshTask = ((Func<Task<object>>)(async () =>
                                {
                                    try
                                    {
                                        var (value, isRecentlyRevoked) = await ExecuteRemoteRequest(key, factory, groupName, logData, metricsKeys, shouldLog, existingItem).ConfigureAwait(false);

                                        lock (existingItem.Lock)
                                        {
                                            if (!isRecentlyRevoked)
                                            {
                                                existingItem.NextRefreshTime = DateTime.UtcNow + policy.RefreshTime;
                                                existingItem.CurrentValueTask = Task.FromResult(value);
                                                MemoryCache.Set(new CacheItem(key, existingItem), policy); //ItemRemovedCallback will be called with Reason=Removed
                                            }
                                            else
                                                existingItem.IsStale = true;
                                        }

                                        return value;
                                    }
                                    catch (Exception e) //do not cache exceptions (we explicitly dont want to cache also RequestException because there are 
                                    {                   //cases of temporal exception, like a get of a just created item, that still not exist because of rc)
                                        lock (existingItem.Lock)
                                        {
                                            //TODO: not sure about this delay, because we will return cached value after ttl,
                                            //but probably it exist so we wont attack remote service which may be overloaded or suffering issues 
                                            existingItem.NextRefreshTime = DateTime.UtcNow + policy.FailedRefreshDelay;
                                        }

                                        throw; 
                                    }
                                })).Invoke();

                            //prevents unhandled exception in case task was not awaited
                            //(can be either awaited if its returned to the caller below, or run in the background and not awaited)
                            existingItem.RefreshTask.ContinueWith(t => { var ignored = t.Exception; }); 
                        }

                        //TODO: test existingItem.CurrentValueTask.IsFaulted logic in different exception flows
                        if (existingItem.IsStale || existingItem.CurrentValueTask.IsFaulted) //return new fetched value and update cache (so stale cache value will not be returned)
                        {
                            resultTask = existingItem.RefreshTask;
                            existingItem.CurrentValueTask = existingItem.RefreshTask;
                            existingItem.IsStale = false; //we want that only one thread will try to fetch a new value (in refreshtask)
                        }
                        else if (shouldSuppressCache) //return new fetched value and ignore cache
                            resultTask = existingItem.RefreshTask;
                        else
                            resultTask = existingItem.CurrentValueTask; //return cached value
                    }

                    if (resultTask.GetAwaiter().IsCompleted)
                         Hits.Mark(metricsKeys);
                    else
                        JoinedTeam.Mark(metricsKeys);
                }
            }

            return resultTask;
        }

        private async Task<(object value, bool isRecentlyRevoked)> ExecuteRemoteRequest(string cacheKey, Func<Task<object>> factory, string groupName, string logData, 
                                                                                        string[] metricsKeys, bool shouldLog, AsyncCacheItem cacheItem)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                if (shouldLog)
                    Log.Info(x => x("Cache item is waiting for value to be resolved", unencryptedTags: new
                    {
                        cacheKey,
                        cacheGroup = groupName,
                        cacheData = logData
                    }));

                object result = null;
                var requestSentTime = DateTime.UtcNow;
                RecentlyRevokeCache.RegisterOutgoingRequest(tcs.Task, requestSentTime);

                result = await factory().ConfigureAwait(false);

                if (shouldLog)
                {
                    Log.Info(x => x("Cache item value is resolved", unencryptedTags: new
                    {
                        cacheKey,
                        cacheGroup = groupName,
                        cacheData = logData,
                        value = GetValueForLogging(result)
                    }));
                }

                string recentlyRevokedKey = null;
                DateTime? recentlyRevokedTime = null;
                var revokeKeys = ((result as IRevocable)?.RevokeKeys)?.ToList() ?? new List<string>();

                lock (cacheItem.Lock) //we want to enforce serial handle of revoke keys, so we wont get to an inconsistent revoke keys state
                {
                    IEnumerable<string> revokeKeysToAdd = null;
                    IEnumerable<string> revokeKeysToRemove = null;

                    if (cacheItem.CurrentValueTask == null     || 
                       !cacheItem.CurrentValueTask.IsCompleted || 
                        cacheItem.CurrentValueTask.IsFaulted   ||
                        cacheItem.CurrentValueTask.IsCanceled  ||
                      !(cacheItem.CurrentValueTask.Result is IRevocable))
                    {
                        revokeKeysToAdd = revokeKeys;
                        revokeKeysToRemove = Enumerable.Empty<string>();
                    }
                    else
                    {
                        var oldRevokeKeys = ((IRevocable) cacheItem.CurrentValueTask.Result).RevokeKeys?.ToList() ?? new List<string>();

                        revokeKeysToAdd = revokeKeys.Except(oldRevokeKeys);
                        revokeKeysToRemove = oldRevokeKeys.Except(revokeKeys);
                    }

                    foreach (var revokeKey in revokeKeysToAdd)
                    {
                        var cacheKeys = RevokeKeyToCacheKeysIndex.GetOrAdd(revokeKey, k => new HashSet<string> { cacheKey });
                        
                        if (!cacheKeys.Contains(cacheKey))
                            lock (cacheKeys)
                            {
                                cacheKeys.Add(cacheKey);
                            }

                        if (shouldLog)
                            Log.Info(x => x("RevokeKey added to reverse index", unencryptedTags: new
                            {
                                revokeKey,
                                cacheKey,
                                cacheGroup = groupName,
                                cacheData = logData
                            }));
                    }

                    foreach (var revokeKey in revokeKeysToRemove)
                    {
                        RemoveRevokeKey(revokeKey, "RemovedFromResponse", cacheItem, cacheKey, shouldLog);
                    }

                    //check if one of the revoke keys was received after the request send time 
                    //resulted from race condition between call and revoke
                    foreach (var revokeKey in revokeKeys)
                    {
                        recentlyRevokedTime = RecentlyRevokeCache.TryGetRecentlyRevokedTime(revokeKey, requestSentTime);
                        if (recentlyRevokedTime != null)
                            recentlyRevokedKey = revokeKey;
                    }
                }

                if (recentlyRevokedTime != null)
                {
                    Items.Meter("RevokeRaceCondition", Unit.Items).Mark();
                    Log.Warn(x => x("Got revoke during request, marking as stale", unencryptedTags: new
                    {
                        cacheGroup = groupName,
                        cacheData  = logData,
                        revokeKey  = recentlyRevokedKey,
                        revokeTime = recentlyRevokedTime,
                        requestSentTime,
                        cacheKey
                    }));
                }

                AwaitingResult.Decrement(metricsKeys);
                return (result, recentlyRevokedTime != null);
            }
            catch (Exception exception)
            {
                Log.Info(x => x("Error resolving value for cache item", unencryptedTags: new
                {
                    cacheKey,
                    cacheGroup = groupName,
                    cacheData = logData,
                    errorMessage = exception.Message
                }));

                AwaitingResult.Decrement(metricsKeys);
                Failed.Mark(metricsKeys);
                throw;
            }
            finally
            {
                tcs.SetResult(true);
            }
        }

        private ConcurrentDictionary<Type, FieldInfo> _revocableValueFieldPerType = new ConcurrentDictionary<Type, FieldInfo>();
        private string GetValueForLogging(object value)
        {
            if (value is IRevocable)
            {
                var revocableValueField = _revocableValueFieldPerType.GetOrAdd(value.GetType(), t=>t.GetField(nameof(Revocable<int>.Value)));
                if (revocableValueField != null)
                    value = revocableValueField.GetValue(value);
            }

            if (value is ValueType || value is string)
                return value.ToString();
            else
                return null;
        }

        /// <summary>
        /// For revocable items , move over all revoke ids in cache index and remove them.
        /// </summary>        
        private void ItemRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            var cacheItem = arguments.CacheItem.Value as AsyncCacheItem;

            var shouldLog = ShouldLog(cacheItem?.GroupName);

            if (shouldLog)
                Log.Info(x=>x("Item removed from cache", unencryptedTags: new
                {
                    cacheKey     = arguments.CacheItem.Key,
                    removeReason = arguments.RemovedReason.ToString(),
                    cacheGroup   = cacheItem?.GroupName,
                    cacheData    = cacheItem?.LogData
                })); 

            var currentValueTask = ((AsyncCacheItem)arguments.CacheItem.Value).CurrentValueTask;

            if(currentValueTask.Status == TaskStatus.RanToCompletion      && 
              (currentValueTask.Result as IRevocable)?.RevokeKeys != null &&
               arguments.RemovedReason != CacheEntryRemovedReason.Removed) //We want to remove items from reverseIndex only if they are not in MemoryCache
            {                                                             //In MemoryCache.Set flow, we get here, but item is still in cache - so we skip removal
                foreach (var revokeKey in ((IRevocable)currentValueTask.Result).RevokeKeys)
                {
                    RemoveRevokeKey(revokeKey, arguments.RemovedReason.ToString(), cacheItem, arguments.CacheItem.Key, shouldLog);
                }
            }

            Items.Meter(arguments.RemovedReason.ToString(), Unit.Items).Mark();
        }

        private void RemoveRevokeKey(string revokeKey, string removeReason, AsyncCacheItem cacheItem, string cacheKey, bool shouldLog)
        {
            if (RevokeKeyToCacheKeysIndex.TryGetValue(revokeKey, out HashSet<string> cacheKeys))
            {
                lock (cacheKeys)
                {
                    var isRemoved = cacheKeys.Remove(cacheKey);

                    if (shouldLog && isRemoved)
                        Log.Info(x => x("RevokeKey removed from reverse index", unencryptedTags: new
                        {
                            cacheKey,
                            revokeKey,
                            removeReason,
                            cacheGroup   = cacheItem?.GroupName,
                            cacheData    = cacheItem?.LogData
                        }));

                    if (!cacheKeys.Any())
                    {
                        if (RevokeKeyToCacheKeysIndex.TryRemove(revokeKey, out _) && shouldLog)
                            Log.Info(x => x("Reverse index for cache item was removed", unencryptedTags: new
                            {
                                cacheKey,
                                revokeKey,
                                removeReason,
                                cacheGroup   = cacheItem?.GroupName,
                                cacheData    = cacheItem?.LogData,
                            }));
                    }
                }
            }
        }

        private bool ShouldLog(string groupName)
        {
            if (groupName == null)
                return false;

            var config = GetRevokeConfig();
            if (config.Groups.TryGetValue(groupName, out var groupConfig))
                return groupConfig.WriteExtraLogs;

            return false;
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
            RevokeDisposable?.Dispose();
        }
    }

}