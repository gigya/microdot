using System;
using System.Linq;
using System.Runtime.ExceptionServices;
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

        private ILog Log { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private Task LoopingTask { get; set; }
        private string DataCenter { get; }
        private string DeploymentIdentifier { get; }
        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }

        public QueryBasedConsulNodeMonitor(string deploymentIdentifier, ILog log, ConsulClient consulClient, IEnvironmentVariableProvider environmentVariableProvider, IDateTime dateTime, Func<ConsulConfig> getConfig)
        {
            DeploymentIdentifier = deploymentIdentifier;
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();

            LoopingTask = LoadNodesLoop();
        }

        /// <inheritdoc />
        public INode[] Nodes 
        {
            get
            {
                if (_disposed > 0)
                    throw new ObjectDisposedException(nameof(ConsulNodeMonitor));

                if (WasUndeployed)
                    throw Ex.ServiceNotDeployed(DataCenter, DeploymentIdentifier);

                if (_nodes.Length == 0 && Error != null)
                {
                    if (Error.StackTrace == null)
                        throw Error;

                    ExceptionDispatchInfo.Capture(Error).Throw();
                }

                return _nodes;
            }           
        }

        /// <inheritdoc />
        public bool WasUndeployed { get; set; } = true;

        private async Task LoadNodesLoop()
        {
            try
            {
                _initTask = LoadNodes();

                while (!ShutdownToken.IsCancellationRequested)
                {
                    await LoadNodes().ConfigureAwait(false);
                    await DateTime.Delay(GetConfig().ReloadInterval, ShutdownToken.Token);
                }
            }
            catch (TaskCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                // Ignore exception during shutdown.
            }
        }

        /// <inheritdoc />
        public Task Init() => _initTask;

        private async Task LoadNodes()
        {
            if (Error != null)
                await DateTime.DelayUntil(ErrorTime + GetConfig().ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);

            string commandPath = $"v1/query/{DeploymentIdentifier}/execute?dc={DataCenter}";
            var consulResult = await ConsulClient.Call<ConsulQueryExecuteResponse>(commandPath, ShutdownToken.Token).ConfigureAwait(false);

            if (!consulResult.IsDeployed)
            {
                WasUndeployed = true;
                _nodes = new INode[0];
            }
            else if (consulResult.Error != null)
            {
                ErrorResult(consulResult);
            }
            else
            {
                ConsulQueryExecuteResponse queryResult = consulResult.Response;
                _nodes = queryResult.Nodes.Select(n => n.ToNode()).ToArray<INode>();
                if (_nodes.Length == 0)
                    ErrorResult(consulResult, "No endpoints were specified in Consul for the requested service and service's active version.");

                WasUndeployed = false;
            }
        }


        private void ErrorResult<T>(ConsulResult<T> result, string errorMessage=null)
        {
            EnvironmentException error = result.Error ?? new EnvironmentException(errorMessage);

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error("Error calling Consul", exception: result.Error, unencryptedTags: new
                {
                    serviceName = DeploymentIdentifier,
                    consulAddress = result.ConsulAddress,
                    commandPath = result.CommandPath,
                    responseCode = result.StatusCode,
                    content = result.ResponseContent
                });
            }

            Error = error;
            ErrorTime = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeAsync().Wait(TimeSpan.FromSeconds(3));
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            if (Interlocked.Increment(ref _disposed) != 1)
                return;
            
            ShutdownToken?.Cancel();
            try
            {
                await LoopingTask.ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }

            ShutdownToken?.Dispose();
        }
    }
}