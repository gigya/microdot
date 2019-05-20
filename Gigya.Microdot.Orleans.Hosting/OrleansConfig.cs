using System.Collections.Concurrent;
using System.Collections.Generic;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class OrleansConfig : IConfigObject
    {
        public string MetricsTableWriteInterval { get; set; } = "00:00:01";
        public double DefaultGrainAgeLimitInMins { get; set; } = 30;
        public IDictionary<string, GrainAgeLimitConfig> GrainAgeLimits { get; set; } = new ConcurrentDictionary<string, GrainAgeLimitConfig>();

        public ZooKeeperConfig ZooKeeper { get; set; }

        public MySqlConfig MySql_v4_0 { get; set; }

    }


    public class ZooKeeperConfig
    {
        public string ConnectionString { get; set; }
    }

    public class MySqlConfig
    {
        public string ConnectionString { get; set; }
    }


    public class GrainAgeLimitConfig
    {
        public double GrainAgeLimitInMins { get; set; }
        public string GrainType { get; set; }

    }
}
