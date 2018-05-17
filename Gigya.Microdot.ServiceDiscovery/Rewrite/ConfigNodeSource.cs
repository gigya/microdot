using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Provides service nodes from configuration. Note: Currently nodes are not specified per environment; it is
    /// assumed they match the current environment and/or are agnostic to environments (i.e. global to the datacenter).
    /// </summary>
    internal class ConfigNodeSource : INodeSource
    {
        private DiscoveryConfig _lastConfig;
        private readonly string _serviceName;
        private INode[] _nodes;

        private Func<DiscoveryConfig> GetConfig { get; }
        private ILog Log { get; }

        private readonly object _updateLocker = new object();

        /// <inheritdoc />
        public bool SupportsMultipleEnvironments => false;

        /// <inheritdoc />
        public ConfigNodeSource(DeploymentIdentifier deployment, Func<DiscoveryConfig> getConfig, ILog log)
        {
            _serviceName = deployment.ServiceName;
            GetConfig = getConfig;
            Log = log;
        }

        /// <inheritdoc />
        public bool WasUndeployed => false;

        /// <inheritdoc />
        public INode[] GetNodes()
        {
            DiscoveryConfig config = GetConfig();

            if (_lastConfig != config)
            {
                lock (_updateLocker)
                {
                    ServiceDiscoveryConfig serviceConfig = config.Services[_serviceName];
                    string hosts = serviceConfig.Hosts ?? string.Empty;
                    _nodes = hosts.Replace("\r", "").Replace("\n", "").Split(',').Where(e => !string.IsNullOrWhiteSpace(e)).Select(_ => _.Trim())
                        .Select(h => CreateNode(h, serviceConfig))
                        .ToArray();

                    Log.Debug(_ => _("Loaded nodes from config. See tags for details.", unencryptedTags: new
                    {
                        configPath = $"Discovery.{_serviceName}",
                        serviceName = _serviceName,
                        nodes = string.Join(",", _nodes.Select(n => n.ToString()))
                    }));
                    _lastConfig = config;
                }
            }

            var nodes = _nodes;
            if (nodes.Length == 0)
                throw Ex.ZeroNodesInConfig(_serviceName);

            return nodes;
        }

        private INode CreateNode(string host, ServiceDiscoveryConfig config)
        {
            var parts = host.Split(':');
            string hostName = parts[0];
            if (parts.Length == 2 && ushort.TryParse(parts[1], out ushort port))
                return new Node(hostName, port);
            else if (parts.Length == 1)
                return new Node(hostName, config.DefaultPort);
            else throw Ex.IncorrectHostFormatInConfig(host, _serviceName);
        }

        public void Shutdown()
        {
            // nothing to shutdown            
        }
    }
}
