using System.Collections.Generic;
using System.Threading;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class ServiceTracingContext : TracingContextBase
    {
        private readonly AsyncLocal<Dictionary<string, object>> _context;

        public ServiceTracingContext()
        {
            _context = new AsyncLocal<Dictionary<string, object>> { Value = new Dictionary<string, object>() };
        }

        public override IDictionary<string, object> Export()
        {
            return _context.Value;
        }

        protected override void Add(string key, object value)
        {
            var values = new Dictionary<string, object>(_context.Value) { [key] = value };
            _context.Value = values;
        }

        protected override T TryGetValue<T>(string key)
        {
            if (_context.Value == null)
            {
                return null;
            }
            _context.Value.TryGetValue(key, out var result);
            return result as T;
        }
    }
}