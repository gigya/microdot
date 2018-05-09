using System;
using System.Collections.Generic;
using System.Linq;
using Gigya.Microdot.Interfaces.HttpService;

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

        private class ValueWrapperToClass
        {
            public DateTimeOffset? DateTime { get; set; }
        }

        public string RequestID
        {
            get => TryGetValue<string>(REQUEST_ID_KEY);
            set => Add(REQUEST_ID_KEY, value);
        }

        public string SpanID => TryGetValue<string>(SPAN_ID_KEY);


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
            get => TryGetValue<ValueWrapperToClass>(SPAN_START_TIME)?.DateTime;
            set => Add(SPAN_START_TIME, new ValueWrapperToClass { DateTime = value });
        }

        /// <summary>
        /// The time at which the topmost API gateway is going to give up on the whole end-to-end request, after which
        /// it makes no sense to try and handle it, or to subsequently call other services.
        /// </summary>
        public DateTimeOffset? AbandonRequestBy
        {
            get => TryGetValue<ValueWrapperToClass>(REQUEST_DEATH_TIME)?.DateTime;
            set => Add(REQUEST_DEATH_TIME, value);
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

        //protected abstract IDictionary<string, object> TracingData { get; set; }

        //public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        //{
        //    return TracingData.GetEnumerator();
        //}

        //IEnumerator IEnumerable.GetEnumerator()
        //{
        //    return ((IEnumerable)TracingData).GetEnumerator();
        //}

        //public void Add(KeyValuePair<string, object> item)
        //{
        //    TracingData.Add(item);
        //}

        //public void Clear()
        //{
        //    TracingData.Clear();
        //}

        //public bool Contains(KeyValuePair<string, object> item)
        //{
        //    return TracingData.Contains(item);
        //}

        //public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        //{
        //    TracingData.CopyTo(array, arrayIndex);
        //}

        //public bool Remove(KeyValuePair<string, object> item)
        //{
        //    return TracingData.Remove(item);
        //}

        //public int Count => TracingData.Count;

        //public bool IsReadOnly => TracingData.IsReadOnly;

        //public bool ContainsKey(string key)
        //{
        //    return TracingData.ContainsKey(key);
        //}

        //public void Add(string key, object value)
        //{
        //    TracingData.Add(key, value);
        //}

        //public bool Remove(string key)
        //{
        //    return TracingData.Remove(key);
        //}

        //public bool TryGetValue(string key, out object value)
        //{
        //    return TracingData.TryGetValue(key, out value);
        //}

        //public object this[string key]
        //{
        //    get => TracingData[key];
        //    set => TracingData[key] = value;
        //}

        //public ICollection<string> Keys => TracingData.Keys;

        //public ICollection<object> Values => TracingData.Values;
    }
}