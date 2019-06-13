using Gigya.Microdot.Interfaces.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class OrleansConfig : IConfigObject
    {
        /// <summary>
        /// Enable microdot load shedding and grain request details.
        /// </summary>
        public bool EnableInterceptor { get; set; } = true;

        public string MetricsTableWriteInterval { get; set; } = "00:00:01";

        public OrleansDashboardConfig DashboardConfig = new OrleansDashboardConfig();
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

    public class OrleansDashboardConfig
    {
        public string WriteInterval { get; set; } = "00:00:30";
        public bool HideTrace { get; set; } = true;
    }
}