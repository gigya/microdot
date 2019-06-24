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
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.SharedLogic.Events
{
    public abstract class TracingContext
    {
        private const string SPAN_ID_KEY = "MD_SpanID";
        private const string PARENT_SPAN_ID_KEY = "MD_SParentSpanID";
        private const string REQUEST_ID_KEY = "MD_SServiceTraceRequestID";
        private const string OVERRIDES_KEY = "MD_SOverrides";
        private const string SPAN_START_TIME = "MD_SSpanStartTime";
        private const string REQUEST_DEATH_TIME = "MD_SRequestDeathTime";

        private T? TryGetNullableValue<T>(string key) where T : struct
        {
            object value = Get(key);
            return value as T?;
        }

        private T TryGetValue<T>(string key) where T : class
        {
            object value = Get(key); ;
            return value as T;
        }

        protected abstract void Set(string key, object value);
        protected abstract object Get(string key);

        internal void SetOverrides(RequestOverrides overrides)
        {
            Set(OVERRIDES_KEY, overrides);
        }

        internal RequestOverrides TryGetOverrides()
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY);
        }

        /// <summary>
        /// Retrieves the host override for the specified service, or returns null if no override was set.
        /// </summary>
        /// <param name="serviceName">The name of the service for which to retrieve the host override.</param>
        /// <returns>A <see cref="HostOverride"/> instance with information about the overriden host for the specified service, or null if no override was set.</returns>
        public HostOverride GetHostOverride(string serviceName)
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY)
                ?.Hosts
                ?.SingleOrDefault(o => o.ServiceName == serviceName);
        }


        public string GetPreferredEnvironment()
        {
            return TryGetValue<RequestOverrides>(OVERRIDES_KEY)?.PreferredEnvironment;
        }

        public void SetPreferredEnvironment(string preferredEnvironment)
        {

            RequestOverrides overrides = (RequestOverrides)Get(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                Set(OVERRIDES_KEY, overrides);
            }

            overrides.PreferredEnvironment = preferredEnvironment;
        }

        public void SetHostOverride(string serviceName, string host, int? port = null)
        {

            var overrides = (RequestOverrides)Get(OVERRIDES_KEY);

            if (overrides == null)
            {
                overrides = new RequestOverrides();
                Set(OVERRIDES_KEY, overrides);
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

        public string TryGetRequestID()
        {
            return TryGetValue<string>(REQUEST_ID_KEY);
        }

        public string TryGetSpanID()
        {
            return TryGetValue<string>(SPAN_ID_KEY);
        }

        public string TryGetParentSpanID()
        {
            return TryGetValue<string>(PARENT_SPAN_ID_KEY);
        }


        /// <summary>
        /// The time at which the request was sent from the client.
        /// </summary>
        public DateTimeOffset? SpanStartTime
        {
            get => TryGetNullableValue<DateTimeOffset>(SPAN_START_TIME);
            set => Set(SPAN_START_TIME, value);
        }

        /// <summary>
        /// The time at which the topmost API gateway is going to give up on the whole end-to-end request, after which
        /// it makes no sense to try and handle it, or to subsequently call other services.
        /// </summary>
        public DateTimeOffset? AbandonRequestBy
        {
            get => TryGetNullableValue<DateTimeOffset>(REQUEST_DEATH_TIME);
            set => Set(REQUEST_DEATH_TIME, value);
        }

        /// <summary>
        /// This add requestID to logical call context in unsafe way (no copy on write)
        /// in order to propagate to parent task. From there on it is immutable and safe.
        /// </summary> 
        public void SetRequestID(string requestID)
        {
            Set(REQUEST_ID_KEY, requestID);
        }

        public void SetSpan(string spanId, string parentSpanId)
        {
            Set(SPAN_ID_KEY, spanId);
            Set(PARENT_SPAN_ID_KEY, parentSpanId);
        }

    }
}