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
using Gigya.Microdot.SharedLogic.Events.Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.SharedLogic.Events
{
    public abstract class TracingContextBase : ITracingContext
    {
        protected const string SPAN_ID_KEY = "SpanID";
        protected const string PARENT_SPAN_ID_KEY = "ParentSpanID";
        protected const string REQUEST_ID_KEY = "ServiceTraceRequestID";
        protected const string OVERRIDES_KEY = "Overrides";

        private const string SPAN_START_TIME = "SpanStartTime";
        private const string REQUEST_DEATH_TIME = "RequestDeathTime";

        [Serializable]
        private class Container<TItem>
        {
            public Container(TItem item) => Item = item;

            public TItem Item { get; }
        }

        public string RequestID
        {
            get => TryGetValue<string>(REQUEST_ID_KEY);
            set => Add(REQUEST_ID_KEY, value);
        }

        public string SpanID => TryGetValue<string>(SPAN_ID_KEY);

        public string PreferredEnvironment
        {
            get => TryGetValue<RequestOverrides>(OVERRIDES_KEY).PreferredEnvironment;
            set => Add(OVERRIDES_KEY, value);
        }


        public string ParentSpnaID => TryGetValue<string>(PARENT_SPAN_ID_KEY);

        public IList<HostOverride> Overrides
        {
            get => TryGetValue<IList<HostOverride>>(OVERRIDES_KEY);
            set => Add(OVERRIDES_KEY, value);
        }

        /// <summary>
        /// The time at which the request was sent from the client.
        /// </summary>
        public DateTimeOffset? SpanStartTime
        {
            get => TryGetValue<Container<DateTimeOffset?>>(SPAN_START_TIME)?.Item;
            set => Add(SPAN_START_TIME, new Container<DateTimeOffset?>(value));
        }

        /// <summary>
        /// The time at which the topmost API gateway is going to give up on the whole end-to-end request, after which
        /// it makes no sense to try and handle it, or to subsequently call other services.
        /// </summary>
        public DateTimeOffset? AbandonRequestBy
        {
            get => TryGetValue<Container<DateTimeOffset?>>(REQUEST_DEATH_TIME)?.Item;
            set => Add(REQUEST_DEATH_TIME, new Container<DateTimeOffset?>(value));
        }

        public abstract IDictionary<string, object> Export();

        public HostOverride GetHostOverride(string serviceName)
        {
            return TryGetValue<IList<HostOverride>>(OVERRIDES_KEY)
                ?.SingleOrDefault(o => o.ServiceName == serviceName);
        }

        public void SetSpan(string spanId, string parentSpanId)
        {
            Add(SPAN_ID_KEY, spanId);
            Add(PARENT_SPAN_ID_KEY, parentSpanId);
        }

        public void SetHostOverride(string serviceName, string host, int? port = null)
        {
            var overrides = TryGetValue<IList<HostOverride>>(OVERRIDES_KEY) ?? new List<HostOverride>();

            var hostOverride = overrides.SingleOrDefault(o => o.ServiceName == serviceName);

            if (hostOverride == null)
            {
                hostOverride = new HostOverride { ServiceName = serviceName, };
                overrides.Add(hostOverride);
            }

            hostOverride.Host = host;
            hostOverride.Port = port;

            Add(OVERRIDES_KEY, overrides);
        }

        protected abstract void Add(string key, object value);

        protected abstract T TryGetValue<T>(string key) where T : class;

    }
}