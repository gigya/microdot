using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Gigya.Microdot.Configuration
{
    public class UsageTracking
    {
        private readonly ConcurrentDictionary<string,Type> _usageTracing = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<string, object> _objectTracking = new ConcurrentDictionary<string, object>();

        public Type Get(string configKey)
        {
            Type result;
            if (_usageTracing.TryGetValue(configKey, out result))
                return result;

            var kvp = _objectTracking.FirstOrDefault(p => configKey.StartsWith(p.Key));
            var prefix = kvp.Key;
            var configObject = kvp.Value;

            if (configObject == null)
                return null;

            var pathParts = configKey.Substring(prefix.Length + 1).Split('.');

            var currentMember = configObject;

            foreach (var pathPart in pathParts.Take(pathParts.Length))
                currentMember = currentMember?.GetType().GetProperty(pathPart)?.GetValue(currentMember);

            return currentMember?.GetType();
        }


        public void Add(string configKey, Type usedAs)
        {
            _usageTracing.AddOrUpdate(configKey, x => usedAs, (k, v) => usedAs);
        }


        public void AddConfigObject(object config, string path)
        {
            _objectTracking[path] = config;
        }
    }
}