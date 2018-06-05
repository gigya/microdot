using System;
using System.Linq;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
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
        private Node[] _nodes;
        private readonly object _updateLocker = new object();

        private Func<DiscoveryConfig> GetConfig { get; }
        private ILog Log { get; }


        /// <inheritdoc />
        public ConfigNodeSource(DeploymentIdentifier deployment, Func<DiscoveryConfig> getConfig, ILog log)
        {
            _serviceName = deployment.ServiceName;
            GetConfig = getConfig;
            Log = log;
        }


        /// <inheritdoc />
        public Node[] GetNodes()
        {
            ReloadNodesIfNeeded();

            var nodes = _nodes;
            if (nodes.Length == 0)
                throw new ServiceUnreachableException(
                    "No nodes were specified in the configuration for the requested service. Please make sure you've specified a list of " + 
                    "hosts for the requested service in the configuration. If you're a developer and want to access a service on your local " +
                    "machine, change service configuration to Discovery.[requestedService].Mode=\"Local\". See tags for the name of the " +
                    "service requested, and for the configuration path where the list of nodes are expected to be specified.",
                    unencrypted: new Tags
                    {
                        { "requestedService", _serviceName },
                        { "missingConfigPath", $"Discovery.{_serviceName}.Hosts" },
                    });

            return nodes;
        }


        private void ReloadNodesIfNeeded()
        {
            var config = GetConfig();

            if (_lastConfig != config)
            {
                lock (_updateLocker)
                {
                    if (_lastConfig != config)
                    {
                        if (!config.Services.TryGetValue(_serviceName, out var serviceConfig))
                            serviceConfig = new ServiceDiscoveryConfig();

                        var newNodes = (serviceConfig.Hosts ?? string.Empty).Replace("\r", "").Replace("\n", "").Split(',')
                            .Select(_ => _.Trim()).Where(_ => !string.IsNullOrEmpty(_)).OrderBy(k => k).Select(_ => CreateNode(_, serviceConfig)).ToArray();

                        if (_nodes == null || !_nodes.SequenceEqual(newNodes))
                        {
                            _nodes = newNodes;
                            Log.Debug(_ => _("Loaded nodes from config.", unencryptedTags: new
                            {
                                configPath  = $"Discovery.{_serviceName}.Hosts",
                                serviceName = _serviceName,
                                nodes       = string.Join(",", _nodes.Select(n => n.ToString()))
                            }));
                        }

                        _lastConfig = config;
                    }
                }
            }
        }


        private Node CreateNode(string host, ServiceDiscoveryConfig config)
        {
            var parts = host.Split(':');
            string hostName = parts[0];
            if (parts.Length == 2 && ushort.TryParse(parts[1], out ushort port))
                return new Node(hostName, port);
            else if (parts.Length == 1)
                return new Node(hostName, config.DefaultPort);
            else throw Ex.IncorrectHostFormatInConfig(host, _serviceName);
        }



        public void Dispose()
        {
            // nothing to shutdown            
        }
    }
}
