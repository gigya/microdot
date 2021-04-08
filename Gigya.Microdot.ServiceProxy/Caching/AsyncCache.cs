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
using System.Runtime.Caching;
using System.Runtime.Caching.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.ServiceContract.HttpService;
using Metrics;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Attributes;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.ServiceDiscovery.Config;

namespace Gigya.Microdot.ServiceProxy.Caching
{

    public sealed class AsyncCache : IMemoryCacheManager, IServiceProvider, IDisposable
    {
        // Main data structures:

        // 1. The main cache that maps a cache key to an actual cached response. Some responses may contain revoke keys.
        //    We use MemoryCache and not a ConcurrentDictionary since it has some nice features like approximating its
        //    RAM usage and evicting items when there's RAM pressure.
        private MemoryCache MemoryCache { get; set; } // maps cache key --> AsyncCacheItem that contains the response.
                                                      // Note that we may cache null responses (as per caching settings).

        // 2. The list of requests in progress. We optionally group concurrent incoming requests for the same item to a single outgoing
        //    request to the service, to reduce load on the target service. Each request returns a tuple with "Ok" denoting whether the
        //    response should be cached as per the caching settings for the method being called.
        private ConcurrentDictionary<string, Lazy<Task<object>>> RequestsInProgress
            = new ConcurrentDictionary<string, Lazy<Task<object>>>();

        // 3. A "reverse index" that maps revoke keys to items in the MemoryCache above. When we receive a revoke message,
        //    we look up that index, find the relevant items in the main cache and remove them or mark them as stale.
        //    Similarly, when an item is removed from the MemoryCache, we remove matching reverse index entries.
        //    The MemoryCache and the reverse index MUST be kept in sync, otherwise we might miss revokes or have a memory leak.
        private ConcurrentDictionary<string /* revoke key */, HashSet<AsyncCacheItem>> RevokeKeyToCacheItemsIndex
            = new ConcurrentDictionary<string, HashSet<AsyncCacheItem>>();

        // 4. Index that keeps track of recent revoke messages. When we get a response from a service, in case
        //    it contains a revoke key that was revoked after having sent the request to the service, we consider the response
        //    stale. This is needed since we can't know ahead of time what revoke keys a response will contain.
        private IRecentRevokesCache RecentRevokesCache { get; set; }


        private IDateTime DateTime { get; }
        private ILog Log { get; }
        private long LastCacheSizeBytes { get; set; }
        private MetricsContext Metrics { get; }

        private MetricsContext Hits { get; set; }
        private MetricsContext Misses { get; set; }
        private MetricsContext JoinedTeam { get; set; }
        private MetricsContext AwaitingResult { get; set; }
        private MetricsContext Failed { get; set; }

        private MetricsContext Items { get; set; }
        private MetricsContext Revokes { get; set; }

        private IDisposable RevokeDisposable { get; }
        private const double MB = 1048576.0;
        private int _clearCount;

        public int RevokeKeysCount => RevokeKeyToCacheItemsIndex.Count;

        /// <summary>
        /// Not thread safe used for testing
        /// </summary>
        internal int CacheKeyCount => RevokeKeyToCacheItemsIndex.Sum(item => item.Value.Count);

        private static long oneTime = 0;

        public AsyncCache(ILog log, MetricsContext metrics, IDateTime dateTime, IRevokeListener revokeListener, 
                          IRecentRevokesCache revokeCache)
        {
            DateTime = dateTime;
            Log = log;
            RecentRevokesCache = revokeCache;

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


        private void InitMetrics()
        {
            // Gauges
            Metrics.Gauge("Size", () => LastCacheSizeBytes / MB, Unit.MegaBytes);
            Metrics.Gauge("Entries", () => MemoryCache.GetCount(), Unit.Items);
            Metrics.Gauge("SizeLimit", () => MemoryCache.CacheMemoryLimit / MB, Unit.MegaBytes);
            Metrics.Gauge("RamUsageLimit", () => MemoryCache.PhysicalMemoryLimit, Unit.Percent);
            Metrics.Gauge("ReverseIndexEntries", () => RevokeKeyToCacheItemsIndex.Count, Unit.Items);
            Metrics.Gauge("ReverseIndexEntriesAndValues", () => RevokeKeyToCacheItemsIndex.Values.Sum(hash => hash.Count), Unit.Items);
            Metrics.Gauge("RequestsInProgress", () => RequestsInProgress.Count, Unit.Items);

            // Counters
            AwaitingResult = Metrics.Context("AwaitingResult");

            // Meters
            Hits = Metrics.Context("Hits");
            Misses = Metrics.Context("Misses");
            JoinedTeam = Metrics.Context("JoinedTeam");
            Failed = Metrics.Context("Failed");

            Items = Metrics.Context("Items");
            Revokes = Metrics.Context("Revoke");
        }


        public Task GetOrAdd(string key, Func<Task> serviceMethod, Type taskResultType, string[] metricsKeys, IMethodCachingSettings settings)
        {
            var getValueTask = Task.Run(() => GetOrAdd(key, () =>
                TaskConverter.ToWeaklyTypedTask(serviceMethod(), taskResultType), metricsKeys, settings));
            return TaskConverter.ToStronglyTypedTask(getValueTask, taskResultType);
        }


        private async Task<object> GetOrAdd(string key, Func<Task<object>> serviceMethod, string[] metricsKeys, IMethodCachingSettings settings)
        {
            AsyncCacheItem cached;
            Task<object> MarkHitAndReturnValue(Task<object> obj) { Hits.Mark(metricsKeys); return obj; }

            // In case caching is suppressed, we don't try to obtain an existing value from the cache, or even wait on an existing request
            // in progress. Meaning we potentially issue multiple concurrent calls to the service (no request grouping).
            if (   TracingContext.CacheSuppress == CacheSuppress.UpToNextServices
                || TracingContext.CacheSuppress == CacheSuppress.RecursiveAllDownstreamServices)

                // If the caching settings specify we shouldn't cache responses when suppressed, then don't.
                if (settings.CacheResponsesWhenSupressedBehavior == CacheResponsesWhenSupressedBehavior.Disabled)
                    return await (await CallService(serviceMethod, metricsKeys)).response;

                // ...otherwise we do put the response in the cache so that subsequent calls to the cache do not revert to the previously-cached value.
                else return await TryFetchNewValue(key, serviceMethod, settings, metricsKeys, CallReason.Suppress);

            // Found a cached response.
            // WARNING! Immediately after calling the line below, another thread may set a new value in the cache, and lines below
            // that call TryFetchNewValue() do it needlessly, causing a redundant refresh operation. This is a rare race
            // condition with negligible negative effects so we can ignore it for the sake of the simplicity of the code.
            else if ((cached = (AsyncCacheItem)MemoryCache.Get(key)) != null)

                // Response was revoked and settings specify we should not use revoked responses
                if (cached.IsRevoked && settings.RevokedResponseBehavior != RevokedResponseBehavior.KeepUsingRevokedResponse)

                    // The caching settings specify we should ignore revoked responses; issue a request.
                    if (settings.RevokedResponseBehavior == RevokedResponseBehavior.FetchNewValueNextTime)
                        return await GroupRequestsIfNeeded(settings, key, metricsKeys, () =>
                            TryFetchNewValue(key, serviceMethod, settings, metricsKeys, CallReason.Revoked));

                    // The caching settings specify we should attempt to fetch a fresh response. If failed, return currently cached value.
                    else if (settings.RevokedResponseBehavior == RevokedResponseBehavior.TryFetchNewValueNextTimeOrUseOld)
                        return await GroupRequestsIfNeeded(settings, key, metricsKeys, () =>
                            TryFetchNewValue(key, serviceMethod, settings, metricsKeys, CallReason.Revoked, cached));

                    // In case RevokedResponseBehavior=TryFetchNewValueInBackgroundNextTime or the enum value is a new option we don't know
                    // how to handle yet, we initiate a background refresh and return the stale response.
                    else {
                        _ = GroupRequestsIfNeeded(settings, key, metricsKeys, () =>
                            TryFetchNewValue(key, serviceMethod, settings, metricsKeys, CallReason.Revoked));
                        return await MarkHitAndReturnValue(cached.Value); // Might throw stored exception
                    }

                // If refreshes are disabled it's because manual revokes are being performed, meaning the current response is up-to-date.
                // Same for UseRefreshesWhenDisconnectedFromCacheRevokesBus, which we currently don't support, and assume everything is okay.
                else if (   settings.RefreshMode == RefreshMode.DoNotUseRefreshes 
                         || settings.RefreshMode == RefreshMode.UseRefreshesWhenDisconnectedFromCacheRevokesBus) //TODO: after DisconnectedFromCacheRevokesBus feature is done, fix this if (to be only when connected)
                    return await MarkHitAndReturnValue(cached.Value); // Might throw stored exception

                // Refreshes are enabled and the refresh time passed
                else if (DateTime.UtcNow >= cached.NextRefreshTime)

                    // Try calling the service to obtain a fresh response. In case of failure return the old response.
                    if (settings.RefreshBehavior == RefreshBehavior.TryFetchNewValueOrUseOld)
                        return await GroupRequestsIfNeeded(settings, key, metricsKeys, () =>
                            TryFetchNewValue(key, serviceMethod, settings, metricsKeys, CallReason.Refresh, cached));

                    // Return the current old response, and trigger a background refresh to obtain a new value.
                    // TODO: In Microdot v4, we'd like to change the default to try and fetch a new value and wait for it (TryFetchNewValueOrUseOld)
                    else {
                        _ = GroupRequestsIfNeeded(settings, key, metricsKeys, () =>
                            TryFetchNewValue(key, serviceMethod, settings, metricsKeys, CallReason.Refresh, cached));
                        return await MarkHitAndReturnValue(cached.Value); // Might throw stored exception
                    }

                // All ok, return cached value
                else return await MarkHitAndReturnValue(cached.Value); // Might throw stored exception

            // No cached response. Call service.
            else return await GroupRequestsIfNeeded(settings, key, metricsKeys, () =>
                TryFetchNewValue(key, serviceMethod, settings, metricsKeys, CallReason.New));
        }


        // This method looks up whether there's a request in progress for the given key. If there is, it returns that ongoing request's task.
        // Otherwise, it triggers a new request by calling the supplied send(), tracks it in RequestsInProgress, and returns that ongoing request task.
        // Once send() is done, it removes it from the list of RequestsInProgress.
        private async Task<object> GroupRequestsIfNeeded(IMethodCachingSettings settings, string key, string[] metricsKeys, Func<Task<object>> send)
        {
            if (settings.RequestGroupingBehavior == RequestGroupingBehavior.Disabled) {
                Misses.Mark(metricsKeys);
                return await send();
            }
            else {
                JoinedTeam.Mark(metricsKeys);
                return await RequestsInProgress.GetOrAdd(key, _ => new Lazy<Task<object>>(async () =>
                {
                    Misses.Mark(metricsKeys);
                    try { return await send(); }
                    finally { RequestsInProgress.TryRemove(key, out var _); }
                })).Value;
            }
        }


        // This method has one of 5 possible outcomes:
        //
        // 1. We got a response from the service (null, not null or an exception) and the caching settings dictate that we should cache it:
        //    We cache it with the default RefreshTime. This extends the ExpirationTime as well.
        //
        // 2. The caching settings dictate that the response shouldn't be cached, and should be ignored, and currentValue != null :
        //    We return the currentValue and set its next refresh time to be now + FailedRefreshDelay so we don't "attack" the target service
        //
        // 3. The caching settings dictate that the response should not be cached, and should be ignored, and currentValue == null :
        //    We return the response anyway, since we don't have a previously-good value
        //
        // 4. The caching settings dictate that the response shouldn't be cached, nor ignored and removed from cache:
        //    We return the response and remove the previously-cached response from the cache
        //
        // 5. The caching settings dictate that the response shouldn't be cached, nor ignored and remain in cache:
        //    We return the response 
        private async Task<object> TryFetchNewValue(string cacheKey, Func<Task<object>> serviceMethod,
            IMethodCachingSettings settings, string[] metricsKeys, CallReason callReason, AsyncCacheItem currentValue = null)
        {
            // We use the RecentRevokesCache to keep track of recent revoke messages that arrived, to detect if the response we're about
            // to receive was revoked while in transit. It will track revoke messages as long as the task is not completed.
            var requestSendTime = DateTime.UtcNow;
            var tcs = new TaskCompletionSource<bool>();
            RecentRevokesCache.RegisterOutgoingRequest(tcs.Task, requestSendTime);

            // We capture the response from the service here, including if it was an exception
            var (response, responseKind) = await CallService(serviceMethod, metricsKeys);

            string outcome = null;

            // Outcome #1: Cache the response
            if (settings.ResponseKindsToCache.HasFlag(responseKind))
            {
                outcome = "cached";
                CacheResponse(cacheKey, requestSendTime, response, settings);
            }
            else if (settings.ResponseKindsToIgnore.HasFlag(responseKind))

                // Outcome #2: Leave old response cached and return it, and set its refresh time to the (short) FailedRefreshDelay
                if (currentValue != null)
                {
                    outcome = "ignored_cachedValueExist";
                    currentValue.NextRefreshTime = DateTime.UtcNow + settings.FailedRefreshDelay.Value;
                    response = currentValue.Value;
                }

                // Outcome #3: We don't have currentValue, so we cant ignore response and we return it
                else
                    outcome = "ignored_cachedValueDoesNotExist";

            else //Dont cache and dont ignore (i.e. return it)
            {
                outcome = "notCachedNotIgnored";

                // Outcome #4: Do not cache response and return it; remove previously-cached value (if exist)
                if (settings.NotIgnoredResponseBehavior == NotIgnoredResponseBehavior.RemoveCachedResponse)
                {
                    if (MemoryCache.Remove(cacheKey) != null)
                        outcome = "notCachedNotIgnored_cachedValueRemoved";
                }

                // Outcome #5: Do not cache response and return it; leave old response cached
                // If old response is not null, set its refresh time to the (short) FailedRefreshDelay
                else if (currentValue != null)
                    currentValue.NextRefreshTime = DateTime.UtcNow + settings.FailedRefreshDelay.Value;
            }

            Log.Debug(x => x("Service call", unencryptedTags: new { cacheKey, callReason, responseKind, outcome }));

            tcs.SetResult(true); // RecentRevokesCache can stop tracking revoke keys
            return await response; // Might throw stored exception
        }



        private async Task<(Task<object> response, ResponseKinds responseKind)> CallService(Func<Task<object>> serviceMethod, string[] metricsKeys)
        {
            Task<object> response; 
            ResponseKinds responseKind;

            try
            {
                AwaitingResult.Increment(metricsKeys);
                
                // Call the service
                response = Task.FromResult(await serviceMethod());

                // Determine the kind of response. We consider it null also in case we got a non-null Revocable<> with null inner Value
                var isNullResponse =    response.Result == null
                                     || (   response.Result.GetType().IsGenericType
                                         && response.Result.GetType().GetGenericTypeDefinition() == typeof(Revocable<>)
                                         && response.Result.GetType().GetProperty("Value").GetValue(response.Result) == null);

                responseKind = isNullResponse ? ResponseKinds.NullResponse : ResponseKinds.NonNullResponse;
            }
            catch (Exception e)
            {
                if (!(e is RequestException))
                    Failed.Mark(metricsKeys);

                // Determine the kind of exception
                responseKind =   e is RequestException ? ResponseKinds.RequestException
                               : e is EnvironmentException ? ResponseKinds.EnvironmentException
                               : e is TimeoutException || e is TaskCanceledException ? ResponseKinds.TimeoutException
                               : ResponseKinds.OtherExceptions;

                response = Task.FromException<object>(e);
                var observed = response.Exception; //dont remove this line as it prevents UnobservedTaskException to be thrown 
            }
            finally
            {
                AwaitingResult.Decrement(metricsKeys);
            }

            return (response, responseKind);
        }



        private void CacheResponse(string cacheKey, DateTime requestSendTime, Task<object> responseTask, IMethodCachingSettings settings)
        {
            var cacheItem = new AsyncCacheItem
            {
                NextRefreshTime = DateTime.UtcNow + settings.RefreshTime.Value,
                Value = responseTask,
            };

            // Register each revoke key in the response with the reverse index, if the response is not an exception and is a Revocable<>.
            // Starting from now, the IsRevoked property in cacheItem might be set to true by another thread in OnRevoked().
            var revokeKeys = ExtarctRevokeKeys(responseTask);

            if (revokeKeys.Any())
                lock (RevokeKeyToCacheItemsIndex)
                    foreach (var revokeKey in revokeKeys)
                        RevokeKeyToCacheItemsIndex.GetOrAdd(revokeKey, _ => new HashSet<AsyncCacheItem>()).Add(cacheItem);

            // Check if we got a revoke message from one of the revoke keys in the response while the request was in progress.
            // Do this AFTER the above, so there's no point in time that we don't monitor cache revokes
            var recentlyRevokedKey = revokeKeys.FirstOrDefault(rk => RecentRevokesCache.TryGetRecentlyRevokedTime(rk, requestSendTime) != null);
            if (recentlyRevokedKey != null)
            {
                Items.Meter("RevokeRaceCondition", Unit.Items).Mark();
                Log.Warn(x => x("Got revoke during request, marking as stale", unencryptedTags: new
                {
                    revokeKey = recentlyRevokedKey,
                    requestSendTime,
                    cacheKey,
                    revokeTime = RecentRevokesCache.TryGetRecentlyRevokedTime(recentlyRevokedKey, requestSendTime)
                }));
                cacheItem.IsRevoked = true;
            }

            // Set the MemoryCache policy based on our caching settings. If the response has no revoke keys, no need to receive a callback
            // event when it's removed from the cache since we don't need to remove it from the reverse index.
            var cacheItemPolicy = new CacheItemPolicy {};
            if (settings.ExpirationBehavior == ExpirationBehavior.ExtendExpirationWhenReadFromCache)
                cacheItemPolicy.SlidingExpiration  = settings.ExpirationTime.Value;
            else 
                cacheItemPolicy.AbsoluteExpiration = DateTime.UtcNow + settings.ExpirationTime.Value;

            if (revokeKeys.Any())
                cacheItemPolicy.RemovedCallback += ItemRemovedCallback;

            // Store the response in the main cache. Note that this will trigger a removal of a currently-cached value, if any,
            // i.e. call ItemRemovedCallback() with Reason=Removed.
            MemoryCache.Set(new CacheItem(cacheKey, cacheItem), cacheItemPolicy);
        }

        /// <summary>
        /// For revocable items, move over all revoke ids in cache index and remove them.
        /// </summary>        
        private void ItemRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            Items.Meter(arguments.RemovedReason.ToString(), Unit.Items).Mark();

            var cacheItem = (AsyncCacheItem)arguments.CacheItem.Value;
            var revokeKeys = ExtarctRevokeKeys(cacheItem.Value);

            //Can lock as revokeKeys > 0
            lock (RevokeKeyToCacheItemsIndex)
                foreach (var revokeKey in revokeKeys)
                    if (   !RevokeKeyToCacheItemsIndex.TryGetValue(revokeKey, out var hash)
                           || !hash.Remove(cacheItem)
                           || (hash.Count == 0 && !RevokeKeyToCacheItemsIndex.TryRemove(revokeKey, out _)))
                        throw new ProgrammaticException("Invalid state");
        }



        private Task OnRevoke(string revokeKey)
        {
            if (revokeKey == null)
                throw new ArgumentNullException();

            RecentRevokesCache.RegisterRevokeKey(revokeKey, DateTime.UtcNow);

            var revokeApplied = false;

            if (RevokeKeyToCacheItemsIndex.TryGetValue(revokeKey, out var _))
                lock (RevokeKeyToCacheItemsIndex)
                    if (RevokeKeyToCacheItemsIndex.TryGetValue(revokeKey, out var hash))
                    {
                        foreach (var item in hash)
                            item.IsRevoked = true;

                        revokeApplied = true;
                    }

            if (revokeApplied)
            {
                Revokes.Meter("Succeeded", Unit.Events).Mark();
                Log.Debug(x => x("Revoke applied", unencryptedTags: new { revokeKey }));
            }
            else
                Revokes.Meter("Discarded", Unit.Events).Mark();

            return Task.CompletedTask;
        }

        private IEnumerable<string> ExtarctRevokeKeys(Task<object> responseTask) => (!responseTask.IsFaulted && !responseTask.IsCanceled ? (responseTask.Result as IRevocable)?.RevokeKeys : null) ?? Enumerable.Empty<string>();

        public void Clear()
        {
            var oldMemoryCache = MemoryCache;
            MemoryCache = new MemoryCache(nameof(AsyncCache) + Interlocked.Increment(ref _clearCount), new NameValueCollection { { "PollingInterval", "00:00:01" } });

            if (oldMemoryCache != null)
            {
                // Disposing of MemoryCache can be a CPU intensive task and should therefore not block the current thread.
                Task.Run(() => oldMemoryCache.Dispose());
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

    public enum CallReason
    {
        New,
        Refresh,
        Suppress,
        Revoked
    }

}