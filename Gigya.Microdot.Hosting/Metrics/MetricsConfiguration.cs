using System;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Hosting.Metrics
{
    [ConfigurationRoot("Metrics", RootStrategy.ReplaceClassNameWithPath)]
    public class MetricsConfiguration: IConfigObject
    {
        public TimeSpan InitializationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
