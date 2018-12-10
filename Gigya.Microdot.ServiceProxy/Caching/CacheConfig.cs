using System;
using System.Collections.Generic;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    [ConfigurationRoot("Cache", RootStrategy.ReplaceClassNameWithPath)]
    public class CacheConfig: IConfigObject
    {
        public bool LogRevokes { get; set; } = false;

        /// <summary>
        /// Configure the interval in ms to clean revokes without associated cache keys (call ahead revokes)
        /// </summary>
        public int RevokesCleanupMs { get; set; } = 600_000;

        public Dictionary<string, CacheGroupConfig> Groups { get; } = new Dictionary<string, CacheGroupConfig>(StringComparer.InvariantCultureIgnoreCase);
    }

    public class CacheGroupConfig
    {
        public bool WriteExtraLogs { get; set; } = false;
    }
}
