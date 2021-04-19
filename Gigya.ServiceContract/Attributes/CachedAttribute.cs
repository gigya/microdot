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

namespace Gigya.Common.Contracts.Attributes
{
    // <summary>
    // Specifies that the method should be cached using the CachingProxy.Set the relevant properties to control caching settings.Note
    // that clients may override these preferences with their own.Also note that clients using older versions of Microdot may ignore some
    // of these settings.
    // </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CachedAttribute : Attribute, IMethodCachingSettings
    {
        public CachedAttribute()
        {
            if (RefreshTime > ExpirationTime)
                throw new ArgumentException("RefreshTime cannot be longer than ExpirationTime");

            if ((ResponseKindsToCache & ResponseKindsToIgnore) > 0)
                throw new ArgumentException("You cannot define a specific response kind both in ResponseKindsToCache and ResponseKindsToIgnore");
        }

        /// <summary>Defines which kinds of responses clients should cache (non-null, null, exceptions, etc). Default: non-null and null
        /// responses. Note that if the client called the method and received a response that needs to be cached (e.g. non-null), then later
        /// refreshes the response and receives a response that shouldn't be cached (e.g. null), it will remove the previous response from
        /// the cache, unless that kind of response is set to be ignored (see <see cref="ResponseKindsToIgnore"/>). Also note that if you
        /// choose to cache exceptions, there's currently no way to revoke them (you can't add revoke keys to exceptions).</summary>
        public ResponseKinds ResponseKindsToCache { get; set; }


        /// <summary>When a client has a cached response but tries to obtain a fresh value (since it was revoked or is considered old), it
        /// will ignore bad responses (e.g. exceptions) and return the last-good value instead. This enum defines which kinds of responses
        /// to ignore. Note that you may not define the same kind both here and in <see cref="ResponseKindsToCache"/>.
        /// Default: all exceptions.</summary>
        public ResponseKinds ResponseKindsToIgnore { get; set; }


        /// <summary>You can either manually revoke responses from client caches (by returning <c>Revocable&lt;&gt;</c> responses and using
        /// an <c>ICacheRevoker</c> to revoke them), or define a time-to-live for cached responses so that clients will periodically fetch
        /// up-to-date responses. Using manual revokes is preferred since revoke messages typically arrive to clients immediately so they
        /// don't keep using stale responses once something was changed, i.e. they'll be "strongly consistent" instead of "eventually
        /// consistent". Using time-to-live will cause clients to use stale responses up to <see cref="RefreshTime"/>, and
        /// they will generate a constant stream of requests (as long as they use particular pieces of data) to fetch up-to-date values,
        /// even if the responses haven't changed.</summary>
        public RefreshMode RefreshMode { get; set; }


        /// <summary>
        /// Default: 1 minute. Once a response was cached, and assuming refreshing is enabled as per <see cref="RefreshMode"/>,
        /// clients will periodically ATTEMPT to fetch a fresh response. If they fail to do so (e.g. in case of a timeout error, and in case
        /// that's not allowed by <see cref="ResponseKindsToCache"/>), then they'll keep using the last-good cached response, up to
        /// <see cref="ExpirationTime"/>, but they will keep retrying to fetch fresh values in the mean time. The higher this value,
        /// the lower the load on your service, and the less up-to-date responses clients will cache. Consider what issues could be caused
        /// by clients using stale responses (they might make wrong decisions based on these responses, or write that stale data somewhere).
        /// </summary>
        public TimeSpan? RefreshTime { get; set; }

        public double RefreshTimeInMinutes
        {
            get => RefreshTime?.TotalMinutes ?? -1;
            set => RefreshTime = value == -1 ? null : (TimeSpan?)TimeSpan.FromMinutes(value);
        }


        /// <summary>
        /// Default: 360 minutes (6 hours). How long should clients cache responses for. When that time passed, responses are evicted from
        /// the cache, unless they were refreshed earlier (see <see cref="RefreshTime"/>), or unless <see cref="ExpirationBehavior"/>
        /// specifies the expiration time is auto-extended when a cached response is used. Responses might be evicted earlier if they were
        /// explicitly revoked or due to RAM pressure on client machines. Note that in case your service is unavailable, incoming requests
        /// will fail and your service clients won't be able to fall back to cached responses after that time, possibly causing them to fail
        /// as well. When picking a value, think how long it might take you to restore your service in case of a production disaster (e.g. 6
        /// hours). But also consider what issues could be caused by clients using very old stale responses (they might make wrong decisions
        /// based on these responses, or write that stale data somewhere).
        /// </summary>
        public TimeSpan? ExpirationTime { get; set; }

        public double ExpirationTimeInMinutes
        {
            get => ExpirationTime?.TotalMinutes ?? -1;
            set => ExpirationTime = value == -1 ? null : (TimeSpan?)TimeSpan.FromMinutes(value);
        }


        /// <summary>
        /// Default: 1 second. When a client calls this method and receives a failure response (e.g. in case of a timeout error, and in case
        /// it should be ignored as per <see cref="ResponseKindsToIgnore"/>), it will not cache the response, and will keep using the last-
        /// good response in the mean time, if any. However, it will wait a shorter delay than <see cref="ExpirationTime"/> till it
        /// attempts to refresh the data, since the response is now considered old and shouldn't be used for much longer. This delay is used
        /// so clients don't "attack" your service when it's already potentially having availability issues. To disable, set to 0.
        /// </summary>
        public TimeSpan? FailedRefreshDelay { get; set; }

        public double FailedRefreshDelayInSeconds
        {
            get => FailedRefreshDelay?.TotalSeconds ?? -1;
            set => FailedRefreshDelay = value == -1 ? null : (TimeSpan?)TimeSpan.FromSeconds(value);
        }


        /// <summary>
        /// Default: Enabled. When a client calls this method multiple times concurrently with the same parameters, the caching layer will
        /// "group" the requests and issue a single request to this method, to reduce the load on this service. It is assumed that this
        /// method returns the exact same answer given the exact same parameters. This flag controls whether to use request grouping or not.
        /// </summary>
        public RequestGroupingBehavior RequestGroupingBehavior { get; set; }


        /// <summary>
        /// Determines what clients do when accessing a cached response that is considered old, i.e. its refresh time passed.
        /// </summary>
        public RefreshBehavior RefreshBehavior { get; set; }


        /// <summary>
        /// Dictates what clients do when a cached response is explicitly revoked by the server.
        /// </summary>
        public RevokedResponseBehavior RevokedResponseBehavior { get; set; }


        /// <summary>
        /// Defines how cached response expiration time is auto-extended.
        /// </summary>
        public ExpirationBehavior ExpirationBehavior { get; set; }


        /// <summary>
        /// Default: Enabled. When clients bypass their cache for specific requests using TracingContext.SuppressCaching(), this flag controls
        /// whether the response will be cached. I.e. clients ignore the cache while READING, but not necessarily when WRITING. 
        /// </summary>
        public CacheResponsesWhenSupressedBehavior CacheResponsesWhenSupressedBehavior { get; set; }

        /// <summary>
        /// Default: Disabled. Defines whether to remove the previously cached response in case a response that we dont want to cache nor to ignore is received 
        /// </summary>
        public NotIgnoredResponseBehavior NotIgnoredResponseBehavior { get; set; }

        // Not in use
        bool? IMethodCachingSettings.Enabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }

    public interface IMethodCachingSettings
    {
        bool? Enabled { get; set; }
        ResponseKinds ResponseKindsToCache { get; set; }
        ResponseKinds ResponseKindsToIgnore { get; set; }
        RefreshMode RefreshMode { get; set; }
        TimeSpan? RefreshTime { get; set; }
        TimeSpan? ExpirationTime { get; set; }
        TimeSpan? FailedRefreshDelay { get; set; }
        RequestGroupingBehavior RequestGroupingBehavior { get; set; }
        RefreshBehavior RefreshBehavior { get; set; }
        RevokedResponseBehavior RevokedResponseBehavior { get; set; }
        ExpirationBehavior ExpirationBehavior { get; set; }
        CacheResponsesWhenSupressedBehavior CacheResponsesWhenSupressedBehavior { get; set; }
        NotIgnoredResponseBehavior NotIgnoredResponseBehavior { get; set; }
    }


    /// <summary>The various kinds of responses that can be obtained from a service (non-null, null, exceptions).</summary>
    [Flags]
    public enum ResponseKinds
    {
        /// <summary>Normal, non-null response. This does not include non-null Revocable&lt;&gt; with a null inner Value.</summary>
        NonNullResponse = 1 << 0,

        /// <summary>Null response. This includes non-null Revocable&lt;&gt; with a null inner Value.</summary>
        NullResponse = 1 << 1,

        /// <summary><see cref="Common.Contracts.Exceptions.RequestException"/> (e.g. 404 not found).</summary>
        RequestException = 1 << 2,

        /// <summary><see cref="Common.Contracts.Exceptions.EnvironmentException"/> (e.g. 500 internal server error).</summary>
        EnvironmentException = 1 << 3,

        /// <summary><see cref="System.TimeoutException"/> or <see cref="System.Threading.Tasks.TaskCanceledException"/>.</summary>
        TimeoutException = 1 << 4,

        /// <summary>Exceptions other than Request, Environment, Timeout and TaskCanceled.</summary>
        OtherExceptions = 1 << 5,
    }


    /// <summary>Controls if and how refreshes should be used.</summary>
    public enum RefreshMode
    {
        /// <summary>Use this option in case this method does not return <c>Revocable&lt;&gt;</c> responses or you don't use an
        /// <c>ICacheRevoker</c> to revoke cached responses. Clients will periodically fetch up-to-date responses.</summary>
        UseRefreshes = 1,

        /// <summary>Use this option in case this method returns Revocable&lt;&gt; responses and you use an ICacheRevoker to revoke cached
        /// responses. Refreshes will NOT be used (since you're revoking responses manually) except in case a client detects that it is
        /// unable to obtain cache revoke messages, in which case it will fall back to using soft expiration until it reconnects to the
        /// revoke messages stream. Note that during that time your service will experience higher incoming traffic.</summary>
        UseRefreshesWhenDisconnectedFromCacheRevokesBus = 2,

        /// <summary>DANGEROUS. Use this option to prevent clients from refreshing responses, up to the time they expire (see
        /// <see cref="CachedAttribute.ExpirationTime"/>).</summary>
        DoNotUseRefreshes = 3,
    }


    /// <summary>
    /// Whether or not to group outgoing requests to the same method with the same parameters.
    /// </summary>
    public enum RequestGroupingBehavior
    {
        Enabled = 1,
        Disabled = 2,
    }


    /// <summary>
    /// Determines what clients do when accessing a cached response that is considered old, i.e. its refresh time passed.
    /// </summary>
    public enum RefreshBehavior
    {
        /// <summary>
        /// When a client encounters a cached response that is older than <see cref="CachedAttribute.RefreshTime"/>, it will attempt to call
        /// this method and fetch a fresh value. If it fails to do so (i.e. the response does not match <see cref="ResponseKinds"/>),
        /// it will keep using the old value. This lets the client to continue providing service while this service is down. It is assumed
        /// that it is preferable to use stale responses over not providing service. The client will retry fetching a new value the next
        /// time it needs that response with a minimum delay of <see cref="CachedAttribute.FailedRefreshDelay"/> between retries, unless it had no
        /// previously-cache response, in which case it might issue a request as soon as it received a reply for the previous one. This is
        /// the default for Microdot v4+ clients.
        /// </summary>
        TryFetchNewValueOrUseOld = 1,


        /// <summary>
        /// DANGEROUS! When a client encounters a cached response that is older than <see cref="CachedAttribute.RefreshTime"/>, it will 
        /// use it even though it's old (possibly several hours old), and will issue a request to obtain a fresh value so the NEXT time it
        /// needs it, it'll be (more) up-to-date (though if it needs it much later, it'll be old anyway). This behavior prioritizes low
        /// latency over fresh data. This is the default for Microdot v1, v2 and v3 clients.
        /// </summary>
        UseOldAndFetchNewValueInBackground = 2,
    }


    /// <summary>
    /// Dictates what clients do when a cached response is explicitly revoked by the server.
    /// </summary>
    public enum RevokedResponseBehavior
    {
        /// <summary>
        /// When a client receives a revoke message, it looks for all cached responses tagged with that message key and marks them
        /// as "stale". The NEXT time the client happens to need such a cached response (if it didn't expire by then), it will ignore
        /// the cached response and call this method to obtain fresh data. If the new response does not match
        /// <see cref="ResponseKinds"/>, it will use the stale cached response. This lets the client to continue providing
        /// service while this service is down. It is assumed that it is preferable to use stale responses over not providing service.
        /// This is the default for Microdot v3+ clients.
        /// </summary>
        TryFetchNewValueNextTimeOrUseOld = 1,

        /// <summary>
        /// DANGEROUS! When a client receives a revoke message, it looks for all cached responses tagged with that message key and marks them
        /// as "stale". With this option, the NEXT time the client happens to need such a cached response (if it didn't expire by then), it
        /// will immediately return the stale response and initiate a background call to the target service to obtain an up-to-date response,
        /// for the next-next time it needs it. Use this option if your clients need the lowest latency possible, even at the cost of using
        /// stale data (that can be several hours out-of-date). This is the default for Microdot v1,v2 and v3 clients.
        /// </summary>
        TryFetchNewValueInBackgroundNextTime = 2,

        /// <summary>
        /// DANGEROUS! With this option, when a client receives a cache revoke message, it will look for all cached responses from that
        /// method tagged with that message key and immediately issue a request to this method to obtain an up-to-date value for each of
        /// them. Use this option if your clients need the lowest latency possible, even at the cost of issuing redundant calls to your
        /// method for data they haven't been using for a while. Use this in case your clients cache and actively use a large portion of
        /// all possible responses from that method.
        /// </summary>
        //TryFetchNewValueImmediately = 3,

        /// <summary>
        /// DANGEROUS! With this option, when a client receives a cache revoke message, it will immediately stop using all cached responses
        /// tagged with that message key. In case your service becomes unavailable the client will not have last-good cached responses
        /// to fall back to, which may cause it to fail, in turn. Use this option in case your clients must absolutely not use stale
        /// responses, even at the cost of not providing service.
        /// </summary>
        FetchNewValueNextTime = 4,

        /// <summary>
        /// DANGEROUS! With this option, when a client receives a cache revoke message, it will ignore the message and keep using stale
        /// responses. You can achieve a similar effect by not returning Revocable&lt;&gt; responses, or responses with an empty list of
        /// revoke keys. If that's inconvenient, you can use this option as a work-around.
        /// </summary>
        KeepUsingRevokedResponse = 5,
    }


    /// <summary>
    /// Defines how cached response expiration time is auto-extended.
    /// </summary>
    public enum ExpirationBehavior
    {
        /// <summary>
        /// This option defines that every time a response is read from the cache, its expiration is pushed forward. This is suitable when
        /// you use manual cache revokes and responses aren't auto-refreshed (since you set <see cref="RefreshMode"/> to
        /// <see cref="RefreshMode.DoNotUseRefreshes"/>), hence their expiration isn't updated, and you don't want them to expire while
        /// they're still in use.
        /// </summary>
        ExtendExpirationWhenReadFromCache = 1,

        /// <summary>
        /// This option defines that cached responses will be expired on time, regardless if they were used or not. This is suitable when
        /// you set <see cref="RefreshMode"/> to <see cref="RefreshMode.UseRefreshes"/>; refresh operations, that occur when clients make
        /// use of cached responses, automatically extend the expiration time.
        /// </summary>
        DoNotExtendExpirationWhenReadFromCache = 2,
    }


    /// <summary>
    /// When clients bypass their cache for specific requests using TracingContext.SuppressCaching(), this option controls
    /// whether the response will be cached. I.e. clients ignore the cache while READING, but not necessarily when WRITING. 
    /// </summary>
    public enum CacheResponsesWhenSupressedBehavior
    {
        Enabled = 1,
        Disabled = 2,
    }

    /// <summary>
    /// Defines whether to remove the previously cached response in case a response that we dont want to cache nor to ignore is received 
    /// </summary>
    public enum NotIgnoredResponseBehavior
    {
        RemoveCachedResponse = 1,
        KeepCachedResponse = 2,
    }
}
