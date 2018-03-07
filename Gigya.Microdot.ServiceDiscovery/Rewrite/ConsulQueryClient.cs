using System;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Client for using Consul with query api
    /// </summary>
    public sealed class ConsulQueryClient : ConsulClientBase, IConsulClient
    {
        public ConsulQueryClient(ILog log, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig):
            base(log,environmentVariableProvider,dateTime,getConfig)
        { }


        public async Task LoadNodes(ConsulServiceState serviceState)
        {
            var consulQuery = $"v1/query/{serviceState.ServiceName}/execute?dc={DataCenter}";
            var response = await CallConsul(consulQuery, serviceState.ShutdownToken).ConfigureAwait(false);

            if (response.IsDeployed == false)
            {
                SetServiceMissingResult(serviceState, response);
                return;
            }
            else if (response.Success)
            {
                var deserializedResponse = TryDeserialize<ConsulQueryExecuteResponse>(response.ResponseContent);
                if (deserializedResponse != null)
                {
                    SetConsulNodes(deserializedResponse.Nodes, serviceState, response, filterByVersion: false);
                    return;
                }
            }

            SetErrorResult(serviceState, response, "Cannot extract service's nodes from Consul query response");
        }
    }
}