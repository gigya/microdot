using System;
using Gigya.Microdot.Interfaces.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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

        public bool EnableTls { get; set; } = false;

        public string OverrideHostNameToUseDuringTlsHandshake { get; set; }

        public ZooKeeperConfig ZooKeeper { get; set; }

        public MySqlConfig MySql_v4_0 { get; set; }

        /// <summary>
        /// Keyed by a category. Corresponding to OrleansLogAdapter Category.
        /// Most probable case is a class name with namespace e.g. Orleans.Runtime.Catalog.
        /// Use _ instead of . Meaning Orleans.Runtime.Catalog is Orleans_Runtime_Catalog.
        /// The log entry will be tagged with IsOrleansLog_b: true.
        /// </summary>
        /// <example>
        /// <![CDATA[
        ///  <CategoryLogLevels>
        ///      <Orleans_Runtime_Catalog LogLevel="None"/>
        ///  </CategoryLogLevels>
        /// ]]>
        /// </example>
        public IDictionary<string, OrleansLogLevel> CategoryLogLevels { get; set; } = new ConcurrentDictionary<string, OrleansLogLevel>();

        /// <summary>
        /// The default of log level, if not changed on the category level.
        /// The default of default: Information.
        /// </summary>
        /// <example>
        /// <![CDATA[
        ///    <DefaultCategoryLogLevel>Information</DefaultCategoryLogLevel>
        /// ]]>
        /// </example>
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel DefaultCategoryLogLevel  { get; set; } = LogLevel.Information;
        
        public TimeSpan? MessageResponseTime { get; set; }
    }

    public class ZooKeeperConfig
    {
        public string ConnectionString { get; set; }
    }

    public class MySqlConfig
    {
        public string ConnectionString { get; set; }
        public string Invariant { get; set; }
    }

    public class OrleansLogLevel
    {
        /// <summary>
        /// Value is <see cref="Microsoft.Extensions.Logging.LogLevel"/>.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel;
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
        public string WriteInterval { get; set; } = "00:00:30"; // Recommended, not less than
        public bool HideTrace { get; set; } = true;
        public bool Enable { get; set; } = true;
    }
}