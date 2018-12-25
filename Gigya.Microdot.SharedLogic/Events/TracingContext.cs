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
using System.Runtime.Remoting.Messaging;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.SharedLogic.Events
{
    public static class TracingContext
    {
        private const string SPAN_ID_KEY = "SpanID";
        private const string PARENT_SPAN_ID_KEY = "ParentSpanID";
        private const string ORLEANS_REQUEST_CONTEXT_KEY = "#ORL_RC";
        private const string REQUEST_ID_KEY = "ServiceTraceRequestID";
        private const string OVERRIDES_KEY = "Overrides";
        private const string SPAN_START_TIME = "SpanStartTime";
        private const string REQUEST_DEATH_TIME = "RequestDeathTime";


        internal static void SetOverrides(RequestOverrides overrides)
        {
            SetValue(OVERRIDES_KEY, overrides);
        }


        internal static RequestOverrides TryGetOverrides()
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY);
        }

        /// <summary>
        /// Retrieves the host override for the specified service, or returns null if no override was set.
        /// </summary>
        /// <param name="serviceName">The name of the service for which to retrieve the host override.</param>
        /// <returns>A <see cref="HostOverride"/> instace with information about the overidden host for the specified service, or null if no override was set.</returns>
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
            SetUpStorage();

            RequestOverrides overrides = TryGetValue<RequestOverrides>(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                SetValue(OVERRIDES_KEY, overrides);
            }

            overrides.PreferredEnvironment = preferredEnvironment;
        }

        public static void SetHostOverride(string serviceName, string host, int? port=null)
        {
            SetUpStorage();

            var overrides = TryGetValue<RequestOverrides>(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                SetValue(OVERRIDES_KEY, overrides);
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

        public static string TryGetSpanID()
        {
            return TryGetValue<string>(SPAN_ID_KEY);
        }

        public static string TryGetParentSpanID()
        {
            return TryGetValue<string>(PARENT_SPAN_ID_KEY);
        }

        /// <summary>
        /// The time at which the request was sent from the client.
        /// </summary>
        public static DateTimeOffset? SpanStartTime
        {
            get => TryGetNullableValue<DateTimeOffset>(SPAN_START_TIME);
            set => CloneAndSetValue(SPAN_START_TIME, value);
        }

        /// <summary>
        /// The time at which the topmost API gateway is going to give up on the whole end-to-end request, after which
        /// it makes no sense to try and handle it, or to subsequently call other services.
        /// </summary>
        public static DateTimeOffset? AbandonRequestBy
        {
            get => TryGetNullableValue<DateTimeOffset>(REQUEST_DEATH_TIME);
            set => SetValue(REQUEST_DEATH_TIME, value);
        }

        /// <summary>
        /// This add requestID to logical call context in unsafe way (no copy on write)
        /// in order to propergate to parent task. From there on it is immutable and safe.
        /// </summary>        
        public static void SetRequestID(string requestID)
        {
            SetValue(REQUEST_ID_KEY, requestID);
        }

        public static void SetSpan(string spanId, string parentSpanId)
        {
            SetValue(SPAN_ID_KEY, spanId);
            SetValue(PARENT_SPAN_ID_KEY, parentSpanId);
        }

        private static void SetValue(string key, object value)
        {
            var context = GetContextData();

            if(context == null)
                throw new InvalidOperationException($"You must call {nameof(SetUpStorage)}() before setting a value on {nameof(TracingContext)}");

            context[key] = value;
        }

        // Implemented better in next Micrdot using Orleans 1.5
        private static void CloneAndSetValue(string key, object value)
        {
            var context = GetContextData();

            if(context == null)
                throw new InvalidOperationException($"You must call {nameof(SetUpStorage)}() before setting a value on {nameof(TracingContext)}");

            var cloned = new Dictionary<string, object>(context);
            cloned[key] = value;
            CallContext.LogicalSetData(ORLEANS_REQUEST_CONTEXT_KEY, cloned);
        }

        private static T TryGetValue<T>(string key) where T : class
        {
            object value = null;
            GetContextData()?.TryGetValue(key, out value);
            return value as T;
        }

        private static T? TryGetNullableValue<T>(string key) where T : struct
        {
            object value = null;
            GetContextData()?.TryGetValue(key, out value);
            return value as T?;
        }

        /// Must setup localstorage in upper most task (one that opens other tasks)
        /// https://stackoverflow.com/questions/31953846/if-i-cant-use-tls-in-c-sharp-async-programming-what-can-i-use
        public static void SetUpStorage()
        {
            if (GetContextData() == null)
            {
                CallContext.LogicalSetData(ORLEANS_REQUEST_CONTEXT_KEY, new Dictionary<string, object>());
            }
        }


        private static Dictionary<string, object> GetContextData()
        {
            return (Dictionary<string, object>)CallContext.LogicalGetData(ORLEANS_REQUEST_CONTEXT_KEY);
        }
    }
}