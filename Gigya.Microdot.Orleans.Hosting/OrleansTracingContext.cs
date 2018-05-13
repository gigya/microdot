using System.Collections.Generic;
using Gigya.Microdot.SharedLogic.Events;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Hosting
{

    public class OrleansTracingContext : TracingContextBase
    {
        private const string MICRODOT_KEY = "MicordotTracingData";

        public override IDictionary<string, object> Export()
        {
            return (IDictionary<string, object>)RequestContext.Get(MICRODOT_KEY);
        }

        protected override void Add(string key, object value)
        {
            var dictionary = (IDictionary<string, object>)RequestContext.Get(MICRODOT_KEY);
            IDictionary<string, object> cloneDictionary = null;

            if (dictionary == null)
                cloneDictionary = new Dictionary<string, object>();
            else
                cloneDictionary = new Dictionary<string, object>(dictionary);

            cloneDictionary[key] = value;
            RequestContext.Set(MICRODOT_KEY, cloneDictionary);
        }

        protected override T TryGetValue<T>(string key)
        {
            var dictionary = (IDictionary<string, object>)RequestContext.Get(MICRODOT_KEY);
            object result = null;

            dictionary?.TryGetValue(key, out result);

            return result as T;
        }

    }
}

