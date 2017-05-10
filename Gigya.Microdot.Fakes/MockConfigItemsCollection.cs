using System;
using System.Collections.Generic;
using System.Linq;

using Gigya.Microdot.Configuration;

namespace Gigya.Microdot.Fakes
{
    internal class MockConfigItemsCollection : ConfigItemsCollection
    {
        private ConfigItemsCollection ConfigItemCollection { get; }

        private readonly Func<Dictionary<string, ConfigItem>> configItemsFunc;

        public MockConfigItemsCollection(Func<Dictionary<string, ConfigItem>> configItems, ConfigItemsCollection configItemCollection=null )
            : base(Enumerable.Empty<ConfigItem>())
        {
            ConfigItemCollection = configItemCollection;

            configItemsFunc = configItems;
        }


        public override IEnumerable<ConfigItem> Items
        {
            get
            {
                var data = configItemsFunc();

                foreach(var item in data)
                {
                    yield return item.Value;
                }

                if(ConfigItemCollection != null)
                {
                    foreach(var item in ConfigItemCollection.Items)
                    {
                        if(!data.ContainsKey(item.Key))
                        {
                            yield return item;
                        }
                    }
                }
            }
        }


        public override ConfigItem TryGetConfigItem(string key)
        {
            key = key.ToLowerInvariant();

            var data = configItemsFunc();
            if (data.ContainsKey(key))
            {
                return data[key];
            }

            return ConfigItemCollection?.TryGetConfigItem(key);
        }
    }
}