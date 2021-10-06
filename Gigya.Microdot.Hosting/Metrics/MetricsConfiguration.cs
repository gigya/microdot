using Gigya.Microdot.Interfaces.Configuration;
using System;

namespace Gigya.Microdot.Hosting.Metrics
{
    [ConfigurationRoot("Metrics", RootStrategy.ReplaceClassNameWithPath)]
    public class MetricsConfiguration: IConfigObject
    {
        public TimeSpan InitializationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
