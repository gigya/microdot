using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Nodes source is set by configuration
    /// </summary>
    public class ConfigNodeSource : INodeSource
    {
        private DiscoveryConfig _lastConfig;
        private readonly string _serviceName;
        private INode[] _nodes;

        private Func<DiscoveryConfig> GetConfig { get; }
        private ILog Log { get; }

        private readonly object _updateLocker = new object();

        /// <inheritdoc />
        public string Type => "Config";

        /// <inheritdoc />
        public bool SupportsMultipleEnvironments => false;

        /// <inheritdoc />
        public ConfigNodeSource(DeploymentIndentifier deployment, Func<DiscoveryConfig> getConfig, ILog log)
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
                    _lastConfig = config;
                    ServiceDiscoveryConfig serviceConfig = config.Services[_serviceName];
                    string hosts = serviceConfig.Hosts ?? string.Empty;
                    _nodes = hosts.Split(',', '\r', '\n')
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .Select(h => CreateNode(h, serviceConfig))
                        .ToArray();

                    Log.Debug(_ => _("Loaded nodes from config. See tags for details.", unencryptedTags: new
                    {
                        configPath = $"Discovery.{_serviceName}",
                        serviceName = _serviceName,
                        nodes = string.Join(",", _nodes.Select(n => n.ToString()))
                    }));
                }
            }

            if (_nodes.Length == 0)
                throw Ex.ZeroNodesInConfig(_serviceName);

            return _nodes;
        }

        private INode CreateNode(string hosts, ServiceDiscoveryConfig config)
        {
            var parts = hosts
                .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => string.IsNullOrEmpty(p) == false)
                .ToArray();

            if (parts.Length > 2)
                throw Ex.IncorrectHostFormatInConfig(hosts);

            string hostName = parts[0];
            int? port = parts.Length == 2 ? int.Parse(parts[1]) : config.DefaultPort;

            return new Node(hostName, port);
        }

        /// <inheritdoc />
        public async Task Init()
        {
            // nothing to init
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // nothing to dispose
        }
    }
}
