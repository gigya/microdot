using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.Interfaces.Logging;

using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Configuration
{
    public class ConfigCache
    {
        public ISourceBlock<ConfigItemsCollection> ConfigChanged => ConfigChangedBroadcastBlock;
        public ConfigItemsCollection LatestConfig { get; private set; }

        private ILog Log { get; }

        private IConfigItemsSource Source { get; }
        private BroadcastBlock<ConfigItemsCollection> ConfigChangedBroadcastBlock { get; }

        public ConfigCache(IConfigItemsSource source, IConfigurationDataWatcher watcher,ILog log)
        {
            Source = source;
            Log = log;
            ConfigChangedBroadcastBlock = new BroadcastBlock<ConfigItemsCollection>(null);

            watcher.DataChanges.LinkTo(new ActionBlock<bool>(Refresh));
            Refresh(false).GetAwaiter().GetResult();
        }


        private async Task Refresh(bool nothing)
        {
            //Prevents faulting of action block
            try
            {
                LatestConfig = await Source.GetConfiguration().ConfigureAwait(false);
                ConfigChangedBroadcastBlock.Post(LatestConfig);
            }
            catch(Exception ex)
            {
                Log.Warn("Error with Refreshing configuration.", exception: ex);
            }            
        }


        public JObject CreateJsonConfig(string path)
        {
            path = path + ".";
            var root = new JObject();

            foreach (var configItem in LatestConfig.Items.Where(kvp => kvp.Key.StartsWith(path)))
            {
                var pathParts = configItem.Key.Substring(path.Length).Split('.');
                var currentObj = root;

                foreach (string pathPart in pathParts.Take(pathParts.Length - 1))
                {
                    var existing = currentObj.GetValue(pathPart, StringComparison.OrdinalIgnoreCase);

                    if (existing == null || existing is JObject == false)
                    {
                        existing = new JObject();
                        currentObj[pathPart] = existing;
                    }

                    currentObj = (JObject)existing;
                }

                currentObj[pathParts.Last()] = configItem.Value;
            }

            return root;
        }
    }
}
