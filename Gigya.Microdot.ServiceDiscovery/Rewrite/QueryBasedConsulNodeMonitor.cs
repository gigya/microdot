using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class QueryBasedConsulNodeMonitor : INodeMonitor
    {
        private CancellationTokenSource ShutdownToken { get; }

        private int _disposed;
        private INode[] _nodes = new INode[0];
        private Task _initTask;

        ILog Log { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }

        public QueryBasedConsulNodeMonitor(string serviceName, ILog log, ConsulClient consulClient, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            ServiceName = serviceName;
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();

            LoadNodesLoop();
        }

        public string DataCenter { get; }

        private string ServiceName { get; }

        public INode[] Nodes 
        {
            get
            {
                if (_nodes.Length==0 && Error != null)
                    throw Error;
                return _nodes;
            }            
        }

        public bool IsDeployed { get; set; } = true;

        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }

        private async Task LoadNodesLoop()
        {
            _initTask = LoadNodes();
            while (!ShutdownToken.IsCancellationRequested)
            {
                await LoadNodes().ConfigureAwait(false);
                await DateTime.Delay(GetConfig().ReloadInterval);
            }
        }

        public Task Init() => _initTask;

        private async Task LoadNodes()
        {
            await WaitIfErrorOccuredOnPreviousCall().ConfigureAwait(false);

            var consulQuery = $"v1/query/{ServiceName}/execute?dc={DataCenter}";
            var consulResult = await ConsulClient.Call<ConsulQueryExecuteResponse>(consulQuery, ShutdownToken.Token).ConfigureAwait(false);

            if (!consulResult.IsDeployed)
            {
                IsDeployed = false;
                _nodes = new INode[0];
            }
            else if (consulResult.Success)
            {
                var queryResult = consulResult.Response;
                _nodes = ConsulClient.ReadConsulNodes(queryResult.Nodes);
                if (_nodes.Length == 0)
                    ErrorResult(consulResult, "No endpoints were specified in Consul for the requested service and service's active version.");

                IsDeployed = true;
            }
            else
                ErrorResult(consulResult, "Cannot extract service's nodes from Consul query response");                        
        }

        private async Task WaitIfErrorOccuredOnPreviousCall()
        {
            if (Error != null)
            {
                var config = GetConfig();
                var now = DateTime.UtcNow;
                var timeElapsed = ErrorTime - now;
                if (timeElapsed < config.ErrorRetryInterval)
                    await DateTime.Delay(config.ErrorRetryInterval - timeElapsed).ConfigureAwait(false);
            }
        }


        private void ErrorResult<T>(ConsulResult<T> result, string errorMessage)
        {
            var error = result.Error ?? new EnvironmentException(errorMessage);

            if (!(error is TaskCanceledException))
                Log.Error("Error calling Consul", exception: result.Error, unencryptedTags: new
                {
                    ServiceName = ServiceName,
                    ConsulAddress = ConsulClient.ConsulAddress.ToString(),
                    consulQuery = result.RequestLog,
                    ResponseCode = result.StatusCode,
                    Content = result.ResponseContent
                });

            Error = error;
            ErrorTime = DateTime.UtcNow;
        }


        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;
            
            ShutdownToken?.Cancel();
            ShutdownToken?.Dispose();
        }
    }
}