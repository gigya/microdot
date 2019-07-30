using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Metrics;

namespace Gigya.Microdot.SharedLogic.Monitor
{   
    /// <remarks>If you ever find you want this class as a pure health status not connected to Metrics.Net,
    /// feel free to split it (i.e. create a new class that only links between Metrics.Net and this health check).</remarks>
    public class PassiveAggregatingHealthCheck : IPassiveAggregatingHealthCheck
    {
        ReportingStrategy _reportingStrategy;
        IDateTime DateTime { get; }

        class HealthStatus
        {
            public bool IsHealthy;
            public string Details;
            public DateTime Expiry;
            public ConcurrentDictionary<string, HealthStatus> Children = new ConcurrentDictionary<string, HealthStatus>();
        }

        HealthStatus _root = new HealthStatus { IsHealthy = true };


        public PassiveAggregatingHealthCheck(IDateTime dateTime, string componentName, IHealthMonitor healthMonitor, ReportingStrategy reportingStrategy)
        {
            _reportingStrategy = reportingStrategy;
            DateTime = dateTime;
            healthMonitor.SetHealthFunction(componentName, GetHealthStatusAndCleanup);
        }


        public void SetBad(string details, TimeSpan timeout, params string[] path)
        {
            Set(false, details, timeout, path);
        }


        public void SetGood(string details, TimeSpan timeout, params string[] path)
        {
            Set(true, details, timeout, path);
        }


        void Set(bool isHealthy, string details, TimeSpan timeout, params string[] path)
        {
            var node = new HealthStatus // update atomically
            {
                IsHealthy = isHealthy,
                Details = details,
                Expiry = DateTime.UtcNow + timeout,
            };
            if (path.Length == 0)
                _root = node;
            else ResolveParent(node.Expiry, path).Children[path.Last()] = node;
        }


        HealthStatus ResolveParent(DateTime expiry, params string[] path)
        {
            HealthStatus parent = _root;
            foreach (var name in path.Take(path.Length - 1))
            {
                parent = parent.Children.GetOrAdd(name,
                    _ => new HealthStatus
                    {
                        Details = name,
                        IsHealthy = true // a parent is always healthy, unless its children are not
                    });
                parent.Expiry = expiry > parent.Expiry ? expiry : parent.Expiry;
            }
            return parent;
        }


        public void Unset(params string[] path)
        {
            if (path.Length == 0)
                throw new InvalidOperationException();
            else ResolveParent(DateTime./*MinValue*/UtcNow, path).Children.TryRemove(path.Last(), out var removed);
        }
        
        private HealthCheckResult GetHealthStatusAndCleanup()
        {
            var sb = new StringBuilder();
            bool healthy = GetHealthStatusAndCleanup(sb, DateTime.UtcNow, "", _root, depth: 0);
            if (healthy)
                return HealthCheckResult.Healthy(sb.ToString());
            else return HealthCheckResult.Unhealthy(sb.ToString());
        }


        private bool GetHealthStatusAndCleanup(StringBuilder sb, DateTime now, string nodeName, HealthStatus node, int depth)
        {
            bool isHealthy = node.IsHealthy;
            sb.Append(new string(' ', depth * 2));
            if (node.Children == null)
                sb.Append(isHealthy ? "[OK]" : "[ERROR]").Append(' ');

            sb.AppendLine(node.Details ?? "");
            
            if (node.Children != null)
            {
                var childrenIsHealthyList = new List<bool>();
                foreach (var child in node.Children)
                {
                    if (child.Value.Expiry <= now)
                        node.Children.TryRemove(child.Key, out var removed);
                    else
                        childrenIsHealthyList.Add(GetHealthStatusAndCleanup(sb, now, child.Key, child.Value, depth + 1));
                }

                if (childrenIsHealthyList.Any())
                {
                    switch (_reportingStrategy)
                    {
                        case ReportingStrategy.UnhealthyOnAtLeastOneChild:
                            isHealthy &= childrenIsHealthyList.TrueForAll(isChildHealthy => isChildHealthy);
                            break;
                        case ReportingStrategy.UnhealthyOnAllChilds:
                            isHealthy &= childrenIsHealthyList.Any(isChildHealthy => isChildHealthy);
                            break;
                        default:
                            throw new InvalidEnumArgumentException(nameof(_reportingStrategy), (int)_reportingStrategy, typeof(ReportingStrategy));
                    }
                }
            }            

            return isHealthy;
        }
    }

    public interface IPassiveAggregatingHealthCheck
    {
        void SetBad(string details, TimeSpan timeout, params string[] path);
        void SetGood(string details, TimeSpan timeout, params string[] path);
        void Unset(params string[] path);
    }

    public enum ReportingStrategy
    {
        UnhealthyOnAtLeastOneChild,
        UnhealthyOnAllChilds
    }
}
