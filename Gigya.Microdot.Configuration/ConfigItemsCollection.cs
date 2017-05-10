using System;
using System.Collections.Generic;

namespace Gigya.Microdot.Configuration
{
    public class ConfigItemsCollection
    {
        private readonly Dictionary<string, ConfigItem> _data = new Dictionary<string, ConfigItem>(StringComparer.OrdinalIgnoreCase);

        public virtual IEnumerable<ConfigItem> Items => _data.Values;

        public ConfigItemsCollection(IEnumerable<ConfigItem> configItems)
        {
            foreach (var configItem in configItems)
            {
                var existing = TryGetConfigItem(configItem.Key);
                if (existing==null || existing.Priority < configItem.Priority)
                    _data[configItem.Key] = configItem;
            }
        }
        
        public virtual ConfigItem TryGetConfigItem(string key)
        {
            ConfigItem configItem;
            _data.TryGetValue(key, out configItem);
            return configItem;
        }

    }
}
