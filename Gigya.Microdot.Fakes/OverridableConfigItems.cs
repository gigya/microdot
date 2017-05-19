using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Fakes
{
    public class OverridableConfigItems :IConfigItemsSource
    {
        private Dictionary<string, string> Data { get; }

        private FileBasedConfigItemsSource FileBasedConfigItemsSource { get; }


        public OverridableConfigItems(FileBasedConfigItemsSource fileBasedConfigItemsSource,
                                        Dictionary<string, string> data)
        {
            FileBasedConfigItemsSource = fileBasedConfigItemsSource;
            Data = data;           
        }


        public OverridableConfigItems(FileBasedConfigItemsSource fileBasedConfigItemsSource)
        {
            FileBasedConfigItemsSource = fileBasedConfigItemsSource;
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<ConfigItemsCollection> GetConfiguration()
        {
            ConfigItemsCollection configItemCollection = null;

            if (FileBasedConfigItemsSource != null)
            configItemCollection = await FileBasedConfigItemsSource.GetConfiguration().ConfigureAwait(false);

            return new MockConfigItemsCollection(GetConfigItemsOverrides, configItemCollection);
        }


        private Dictionary<string, ConfigItem> GetConfigItemsOverrides()
        {
            var items = new Dictionary<string, ConfigItem>(StringComparer.CurrentCultureIgnoreCase);
            foreach(var item in Data)
            {
                items.Add(item.Key, new ConfigItem
                {
                    Key = item.Key, Value = item.Value,
                    Overrides = new List<ConfigItemInfo>
                    {
                        new ConfigItemInfo {
                            FileName = @"c:\\dumy.config",
                            Priority = 1,
                            Value = item.Value}
                    }
                });
            }
            return items;
        }


        public void SetValue(string key, string value)
        {
            Data[key] = value;
        }
    }
}