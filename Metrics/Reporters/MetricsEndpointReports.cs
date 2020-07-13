using System;
using System.Collections.Generic;
using Metrics.Endpoints;
using Metrics.MetricData;
using Metrics.Reporters;

namespace Metrics.Reports
{
    public sealed class MetricsEndpointReports : Utils.IHideObjectMembers
    {
        private readonly MetricsDataProvider metricsDataProvider;
        private readonly Func<HealthStatus> healthStatus;

        private readonly Dictionary<string, MetricsEndpoint> endpoints = new Dictionary<string, MetricsEndpoint>();

        public IEnumerable<MetricsEndpoint> Endpoints => this.endpoints.Values;

        public MetricsEndpointReports(MetricsDataProvider metricsDataProvider, Func<HealthStatus> healthStatus)
        {
            this.metricsDataProvider = metricsDataProvider;
            this.healthStatus = healthStatus;
            RegisterDefaultEndpoints();
        }

        /// <summary>
        /// Register a report at the specified endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint where the report will be accessible. E.g. "/text" </param>
        /// <param name="responseFactory">Produces the response. Will be called each time the endpoint is accessed.</param>
        /// <returns>Chain-able configuration object.</returns>
        public MetricsEndpointReports WithEndpointReport(string endpoint, Func<MetricsData, Func<HealthStatus>, MetricsEndpointRequest, MetricsEndpointResponse> responseFactory)
        {
            var metricsEndpoint = new MetricsEndpoint(endpoint, r => responseFactory(this.metricsDataProvider.CurrentMetricsData, this.healthStatus, r));
            this.endpoints[metricsEndpoint.Endpoint] = metricsEndpoint;
            return this;
        }

        private void RegisterDefaultEndpoints()
        {
            this
                .WithTextReport("/text")
                .WithJsonHealthReport("/health")
                .WithJsonHealthReport("/v1/health")
                .WithJsonV1Report("/v1/json")
                .WithJsonV2Report("/v2/json")
                .WithJsonReport("/json")
                .WithPing();
        }
    }
}
