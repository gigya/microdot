using Gigya.Microdot.Interfaces.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class OrleansConfig : IConfigObject
    {
        /// <summary>
        /// Enable microdot load shedding and grain request details.
        /// </summary>
        public bool EnableInterceptor { get; set; } = true;

        public string MetricsTableWriteInterval { get; set; } = "00:00:01";

        public DashboardConfig Dashboard { get; set; } = new DashboardConfig(); // We need initialization, else will be null, and no default will be available
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
        /// <remarks>
        /// The CollectionAgeLimit must be greater than CollectionQuantum, which is set to 00:01:00 (by default).
        /// https://dotnet.github.io/orleans/Documentation/clusters_and_clients/configuration_guide/activation_garbage_collection.html
        /// See CollectionAgeLimitValidator.cs details.
        /// </remarks>
        [Range(1.001d, double.MaxValue, ErrorMessage = "The GrainAgeLimitInMins " +
                                                       "(CollectionAgeLimit) must be greater than CollectionQuantum, " +
                                                       "which is set to 1 min (by default). The type is double.")]
        public double GrainAgeLimitInMins { get; set; }

        /// <summary>
        /// The full qualified type name to apply grain age limit.
        /// </summary>
        public string GrainType { get; set; }
    }

    public class DashboardConfig
    {
        public string WriteInterval { get; set; } = "00:00:30";
        public bool HideTrace { get; set; } = true;
        public bool Enable { get; set; } = true;
    }
}