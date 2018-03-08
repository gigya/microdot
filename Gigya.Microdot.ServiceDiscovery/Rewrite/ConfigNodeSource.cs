using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Exceptions;
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

        public string Type => "Config";

        public bool SupportsMultipleEnvironments => false;

        public ConfigNodeSource(ServiceDeployment deployment, Func<DiscoveryConfig> getConfig, ILog log)
        {
            _serviceName = deployment.ServiceName;
            GetConfig = getConfig;
            Log = log;
        }

        public bool WasUndeployed => false;

        public INode[] GetNodes()
        {
            var config = GetConfig();
            if (_lastConfig == config)
                return GetNonEmptyNodesList();

            lock (_updateLocker)
            {
                _lastConfig = config;
                var serviceConfig = config.Services[_serviceName];
                var hosts = serviceConfig.Hosts ?? string.Empty;
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

            return GetNonEmptyNodesList();
        }

        private INode[] GetNonEmptyNodesList()
        {
            if (_nodes.Length==0)
                throw new EnvironmentException("No nodes were specified in the configuration for the " +
                                                "requested service. Please make sure you've specified a list of hosts for the requested " +
                                                "service in the configuration. If you're a developer and want to access a service on your " +
                                                "local machine, change service configuration to Discovery.[ServiceName].Mode=\"Local\". " +
                                                "See tags for the name of the service requested, and for the " +
                                                "configuration path where the list of nodes are expected to be specified.",
                    unencrypted: new Tags
                    {
                        {"requestedService", _serviceName},
                        {"missingConfigPath", $"Discovery.{_serviceName}.Hosts"}
                    });

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
                throw new ConfigurationException("Host name must contain at most one colon (:).", unencrypted: new Tags{{"hosts", hosts}});

            var hostName = parts[0];
            var port = parts.Length == 2 ? int.Parse(parts[1]) : config.DefaultPort;

            return new Node(hostName, port);
        }

        public async Task Init()
        {
            // nothing to init
        }

        public void Dispose()
        {
            // nothing to dispose
        }
    }
}
