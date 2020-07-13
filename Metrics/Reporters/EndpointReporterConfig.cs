using System;
using System.Linq;
using System.Text;
using Metrics.Endpoints;
using Metrics.Json;
using Metrics.MetricData;
using Metrics.Reports;

namespace Metrics.Reporters
{
    public static class EndpointReporterConfig
    {
        public static MetricsEndpointReports WithTextReport(this MetricsEndpointReports reports, string endpoint)
        {
            return reports.WithEndpointReport(endpoint, (d, h, c) => new MetricsEndpointResponse(StringReport.RenderMetrics(d, h), "text/plain"));
        }

        public static MetricsEndpointReports WithJsonHealthReport(this MetricsEndpointReports reports, string endpoint, bool alwaysReturnOkStatusCode = false)
        {
            return reports.WithEndpointReport(endpoint, (d, h, r) => GetHealthResponse(h, alwaysReturnOkStatusCode));
        }

        private static MetricsEndpointResponse GetHealthResponse(Func<HealthStatus> healthStatus, bool alwaysReturnOkStatusCode)
        {
            var status = healthStatus();
            var json = JsonHealthChecks.BuildJson(status);

            var httpStatus = status.IsHealthy || alwaysReturnOkStatusCode ? 200 : 500;
            var httpStatusDescription = status.IsHealthy || alwaysReturnOkStatusCode ? "OK" : "Internal Server Error";

            return new MetricsEndpointResponse(json, JsonHealthChecks.HealthChecksMimeType, Encoding.UTF8, httpStatus, httpStatusDescription);
        }

        public static MetricsEndpointReports WithJsonV1Report(this MetricsEndpointReports reports, string endpoint)
        {
            return reports.WithEndpointReport(endpoint, GetJsonV1Response);
        }

        private static MetricsEndpointResponse GetJsonV1Response(MetricsData data, Func<HealthStatus> healthStatus, MetricsEndpointRequest request)
        {
            var json = JsonBuilderV1.BuildJson(data);
            return new MetricsEndpointResponse(json, JsonBuilderV1.MetricsMimeType);
        }

        public static MetricsEndpointReports WithJsonV2Report(this MetricsEndpointReports reports, string endpoint)
        {
            return reports.WithEndpointReport(endpoint, GetJsonV2Response);
        }

        private static MetricsEndpointResponse GetJsonV2Response(MetricsData data, Func<HealthStatus> healthStatus, MetricsEndpointRequest request)
        {
            var json = JsonBuilderV2.BuildJson(data);
            return new MetricsEndpointResponse(json, JsonBuilderV2.MetricsMimeType);
        }

        public static MetricsEndpointReports WithJsonReport(this MetricsEndpointReports reports, string endpoint)
        {
            return reports.WithEndpointReport(endpoint, GetJsonResponse);
        }

        public static MetricsEndpointReports WithPing(this MetricsEndpointReports reports)
        {
            return reports.WithEndpointReport("/ping", (d, h, r) => new MetricsEndpointResponse("pong", "text/plain"));
        }

        private static MetricsEndpointResponse GetJsonResponse(MetricsData data, Func<HealthStatus> healthStatus, MetricsEndpointRequest request)
        {
            string[] acceptHeader;
            if (request.Headers.TryGetValue("Accept", out acceptHeader))
            {
                return acceptHeader.Contains(JsonBuilderV2.MetricsMimeType)
                    ? GetJsonV2Response(data, healthStatus, request)
                    : GetJsonV1Response(data, healthStatus, request);
            }

            return GetJsonV1Response(data, healthStatus, request);
        }
    }
}
