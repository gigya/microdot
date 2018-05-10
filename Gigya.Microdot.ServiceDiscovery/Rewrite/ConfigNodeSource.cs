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
        public ConfigNodeSource(DeploymentIdentifier deployment, Func<DiscoveryConfig> getConfig, ILog log)
        {
            _serviceName = deployment.ServiceName; // we don't need the environment
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
                _lastConfig = config; // do last or other threads will take _nodes before they're ready
                    ServiceDiscoveryConfig serviceConfig = config.Services[_serviceName];
                    string hosts = serviceConfig.Hosts ?? string.Empty;
                    _nodes = hosts.Split(',', '\r', '\n')
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        // trim?
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

            return _nodes; // not atomic; could be empty or null
        }

        private INode CreateNode(string hosts, ServiceDiscoveryConfig config)
        {
            var parts = hosts // singular host
                .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries) // why remove?
                .Select(p => p.Trim()) // so "us1a-nomad01      :1234" is legal?
                .Where(p => string.IsNullOrEmpty(p) == false) // remove?
                .ToArray();

            if (parts.Length > 2)
                throw Ex.IncorrectHostFormatInConfig(hosts);  // add config path

            string hostName = parts[0];
            int? port = parts.Length == 2 ? int.Parse(parts[1]) : config.DefaultPort; // check parse failure, write config path

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

        public Task DisposeAsync()
        {
            // nothing to dispose
            return Task.FromResult(true);            
        }
    }
}
