using System;
using System.Collections.Generic;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    [ConfigurationRoot("Cache", RootStrategy.ReplaceClassNameWithPath)]
    public class CacheConfig: IConfigObject
    {
        public bool DontCacheRecentlyRevokedResponses { get; set; } = true;
        public int DelayBetweenRecentlyRevokedCacheClean { get; set; } = 1000;
        public bool LogRevokes { get; set; } = false;
        public Dictionary<string, CacheGroupConfig> Groups { get; } = new Dictionary<string, CacheGroupConfig>(StringComparer.InvariantCultureIgnoreCase);
    }

    public class CacheGroupConfig
    {
        public bool WriteExtraLogs { get; set; } = false;
    }
}
