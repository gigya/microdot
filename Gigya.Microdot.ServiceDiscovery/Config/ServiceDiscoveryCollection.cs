using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    public class ServiceDiscoveryCollection : IImmutableDictionary<string, ServiceDiscoveryConfig>
    {
        private IImmutableDictionary<string, ServiceDiscoveryConfig> Source { get; }
        private ServiceDiscoveryConfig DefaultItem { get; }


        public ServiceDiscoveryConfig this[string key]
        {
            get
            {
                ServiceDiscoveryConfig item;
                TryGetValue(key, out item);
                return item;
            }
        }


        public ServiceDiscoveryCollection(IDictionary<string, ServiceDiscoveryConfig> source, ServiceDiscoveryConfig defaultItem, PortAllocationConfig portAllocationConfig)
        {
            DefaultItem = defaultItem;
            Source = source.ToDictionary(kvp => kvp.Key, kvp => ApplyDefaults(kvp.Value, portAllocationConfig)).ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        }


        private ServiceDiscoveryConfig ApplyDefaults(ServiceDiscoveryConfig item, PortAllocationConfig portAllocationConfig)
        {
            item.ReloadInterval           = item.ReloadInterval           ?? DefaultItem.ReloadInterval;
            item.DelayMultiplier          = item.DelayMultiplier          ?? DefaultItem.DelayMultiplier;
            item.FirstAttemptDelaySeconds = item.FirstAttemptDelaySeconds ?? DefaultItem.FirstAttemptDelaySeconds;
            item.MaxAttemptDelaySeconds   = item.MaxAttemptDelaySeconds   ?? DefaultItem.MaxAttemptDelaySeconds;
            item.RequestTimeout           = item.RequestTimeout           ?? DefaultItem.RequestTimeout;
            item.Scope                    = item.Scope                    ?? DefaultItem.Scope;
            item.Source                   = item.Source                   ?? DefaultItem.Source;

            if (portAllocationConfig.IsSlotMode && item.DefaultSlotNumber.HasValue)
                item.DefaultPort = portAllocationConfig.GetPort(item.DefaultSlotNumber, PortOffsets.Http);

            return item;
        }


        public bool Contains(KeyValuePair<string, ServiceDiscoveryConfig> item)
        {
            if (Source.ContainsKey(item.Key) == false)
                return true;

            return Source.Contains(item);
        }


        public bool TryGetKey(string equalKey, out string actualKey)
        {
            actualKey = equalKey;
            return true;
        }


        public bool ContainsKey(string key) => true;


        public bool TryGetValue(string key, out ServiceDiscoveryConfig value)
        {
            if (Source.TryGetValue(key, out value) == false)
                value = DefaultItem;

            return true;
        }

        #region IDictionary delegation

        public IEnumerator<KeyValuePair<string, ServiceDiscoveryConfig>> GetEnumerator() => Source.GetEnumerator();
        public IImmutableDictionary<string, ServiceDiscoveryConfig> Clear() => Source.Clear();
        public IImmutableDictionary<string, ServiceDiscoveryConfig> Add(string key, ServiceDiscoveryConfig value) => Source.Add(key, value);
        public IImmutableDictionary<string, ServiceDiscoveryConfig> AddRange(IEnumerable<KeyValuePair<string, ServiceDiscoveryConfig>> pairs) => Source.AddRange(pairs);
        public IImmutableDictionary<string, ServiceDiscoveryConfig> SetItem(string key, ServiceDiscoveryConfig value) => Source.SetItem(key, value);
        public IImmutableDictionary<string, ServiceDiscoveryConfig> SetItems(IEnumerable<KeyValuePair<string, ServiceDiscoveryConfig>> items) => Source.SetItems(items);
        public IImmutableDictionary<string, ServiceDiscoveryConfig> RemoveRange(IEnumerable<string> keys) => Source.RemoveRange(keys);
        public IImmutableDictionary<string, ServiceDiscoveryConfig> Remove(string key) => Source.Remove(key);

        public IEnumerable<string> Keys => Source.Keys;

        public IEnumerable<ServiceDiscoveryConfig> Values => Source.Values;


        public int Count => Source.Count;


        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Source).GetEnumerator();
        }


        #endregion


    }
}
