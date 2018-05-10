using System;
using System.Collections.Generic;
using System.Threading;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class ServiceTracingContext : TracingContextBase
    {
        private AsyncLocal<Dictionary<string, object>> Context { get; } = new AsyncLocal<Dictionary<string, object>>();

        public override IDictionary<string, object> Export()
        {
            return Context.Value;
        }

        protected override void Add(string key, object value)
        {
            if (Context.Value == null)
                Context.Value = new Dictionary<string, object>();
            else
                Context.Value = new Dictionary<string, object>(Context.Value);

            Context.Value[key] = value;
        }

        protected override T TryGetValue<T>(string key)
        {
            if (Context.Value == null)
            {
                return null;
            }

            Context.Value.TryGetValue(key, out var result);
            return result as T;
        }
    }
}