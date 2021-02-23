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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.SharedLogic.Events
{
    public static class TracingContext
    {
        static TracingContext()
        {
            Implementation = new TracingContextSourcev();
        }

        internal static TracingContextSourcev Implementation;

        private const string PARENT_SPAN_ID_KEY    = "MD_SParentSpanID";
        private const string REQUEST_ID_KEY        = "MD_SServiceTraceRequestID";
        private const string OVERRIDES_KEY         = "MD_SOverrides";
        private const string SPAN_START_TIME       = "MD_SSpanStartTime";
        private const string REQUEST_DEATH_TIME    = "MD_SRequestDeathTime";
        private const string ADDITIONAL_PROPERTIES = "AdditionalProperties";
        private const string TAGS_KEY              = "MD_Tags";

        public static void ClearContext()
        {
            Implementation.Set(PARENT_SPAN_ID_KEY, null);
            Implementation.Set(REQUEST_ID_KEY, null);
            Implementation.Set(OVERRIDES_KEY, null);
            Implementation.Set(SPAN_START_TIME, null);
            Implementation.Set(REQUEST_DEATH_TIME, null);
            Implementation.Set(ADDITIONAL_PROPERTIES, null);
            Implementation.Set(TAGS_KEY, null);
        }


        private static T? TryGetNullableValue<T>(string key) where T : struct
        {
            object value = Implementation.Get(key);
            return value as T?;
        }

        private static T TryGetValue<T>(string key) where T : class
        {
            object value = Implementation.Get(key); ;
            return value as T;
        }

        private static T GetOrCreateValue<T>(string key) where T : class, new()
        {
            object value = Implementation.Get(key);
            if (value == null)
                Implementation.Set(key, value = new T());
            return value as T;
        }

        internal static void SetOverrides(RequestOverrides overrides)
        {
            Implementation.Set(OVERRIDES_KEY, overrides);
        }

        internal static RequestOverrides TryGetOverrides()
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY);
        }

        /// <summary>
        /// Retrieves the host override for the specified service, or returns null if no override was set.
        /// </summary>
        /// <param name="serviceName">The name of the service for which to retrieve the host override.</param>
        /// <returns>A <see cref="HostOverride"/> instance with information about the overriden host for the specified service, or null if no override was set.</returns>
        public static HostOverride GetHostOverride(string serviceName)
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY)
                ?.Hosts
                ?.SingleOrDefault(o => o.ServiceName == serviceName);
        }


        public static string GetPreferredEnvironment()
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY)?.PreferredEnvironment;
        }

        public static void SetPreferredEnvironment(string preferredEnvironment)
        {

            RequestOverrides overrides = (RequestOverrides)Implementation.Get(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                Implementation.Set(OVERRIDES_KEY, overrides);
            }

            overrides.PreferredEnvironment = preferredEnvironment;
        }

        public static void SetHostOverride(string serviceName, string host, int? port = null)
        {

            var overrides = (RequestOverrides)Implementation.Get(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                Implementation.Set(OVERRIDES_KEY, overrides);
            }

            if (overrides.Hosts == null)
                overrides.Hosts = new List<HostOverride>();

            var hostOverride = overrides.Hosts.SingleOrDefault(o => o.ServiceName == serviceName);

            if (hostOverride == null)
            {
                hostOverride = new HostOverride { ServiceName = serviceName, };
                overrides.Hosts.Add(hostOverride);
            }

            hostOverride.Host = host;
            hostOverride.Port = port;
        }

        public static string TryGetRequestID()
        {
            return TryGetValue<string>(REQUEST_ID_KEY);
        }

        public static string TryGetParentSpanID()
        {
            return TryGetValue<string>(PARENT_SPAN_ID_KEY);
        }


        /// <summary>
        /// warning: CacheSuppress value of 'RecursiveAllDownstreamServices' will cause all downstream services to not use caching and might cause them performance issues.
        /// It should be used sparingly.
        /// </summary>
        public static IDisposable SuppressCaching(CacheSuppress cacheSuppress)
        {
            var overrides = (RequestOverrides)Implementation.Get(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                Implementation.Set(OVERRIDES_KEY, overrides);
            }

            var prevCacheSuppress = overrides.SuppressCaching;
            overrides.SuppressCaching = cacheSuppress;

            return new DisposableAction<CacheSuppress?>(prevCacheSuppress, x =>
            {
                ((RequestOverrides) Implementation.Get(OVERRIDES_KEY)).SuppressCaching = x;
            });
        }

        public static CacheSuppress? CacheSuppress => TryGetValue<RequestOverrides>(OVERRIDES_KEY)?.SuppressCaching;

        /// <summary>
        /// The time at which the request was sent from the client.
        /// </summary>
        public static DateTimeOffset? SpanStartTime
        {
            get => TryGetNullableValue<DateTimeOffset>(SPAN_START_TIME);
            set => Implementation.Set(SPAN_START_TIME, value);
        }

        /// <summary>
        /// The time at which the topmost API gateway is going to give up on the whole end-to-end request, after which
        /// it makes no sense to try and handle it, or to subsequently call other services.
        /// </summary>
        public static DateTimeOffset? AbandonRequestBy
        {
            get => TryGetNullableValue<DateTimeOffset>(REQUEST_DEATH_TIME);
            set => Implementation.Set(REQUEST_DEATH_TIME, value);
        }

        public static Dictionary<string, object> AdditionalProperties
        {
            get => TryGetValue<Dictionary<string, object>>(ADDITIONAL_PROPERTIES);
            set => Implementation.Set(ADDITIONAL_PROPERTIES, value);
        }

        public static ContextTags Tags
        {
            get => GetOrCreateValue<ContextTags>(TAGS_KEY);
            set => Implementation.Set(TAGS_KEY, value);
        }

        internal static ContextTags TagsOrNull
        {
            get => TryGetValue<ContextTags>(TAGS_KEY);
        }

        /// <summary>
        /// This add requestID to logical call context in unsafe way (no copy on write)
        /// in order to propagate to parent task. From there on it is immutable and safe.
        /// </summary> 
        public static void SetRequestID(string requestID)
        {
            Implementation.Set(REQUEST_ID_KEY, requestID);
        }

        public static void SetParentSpan(string parentSpanId)
        {
       
            Implementation.Set(PARENT_SPAN_ID_KEY, parentSpanId);
        }

    }

    public class TracingContextSourcev
    {
        private readonly TracingContextNoneOrleans fallback;

        private readonly MethodInfo _getMethodInfo;
        Action<string, object> setter;
        Func<string, object> getter;

        public TracingContextSourcev()
        {
            fallback = new TracingContextNoneOrleans();

            var type = Type.GetType("Orleans.Runtime.RequestContext, Orleans.Core.Abstractions", throwOnError: false);
            if(type != null)
            {
                var setMethodInfo = type.GetMethod("Set", BindingFlags.Static | BindingFlags.Public);
                _getMethodInfo =  type.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);

                setter = (key, value) =>
                {
                    setMethodInfo.Invoke(null, new[]{key, value});
                };

                getter = (key) =>
                {
                    return _getMethodInfo.Invoke(null, new []{key});
                };
            }
        }

        public void Set(string key, object value)
        {
            if (setter != null)
                setter(key, value);
            else
                fallback.Set(key, value);
        }

        public object Get(string key)
        {
            if (getter != null)
                return getter(key);
            
            return fallback.Get(key);
        }
    }

    public enum CacheSuppress
    {
        DoNotSuppress,
        UpToNextServices,
        RecursiveAllDownstreamServices
    }
}