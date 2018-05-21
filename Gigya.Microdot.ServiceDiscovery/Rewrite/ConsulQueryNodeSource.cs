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
    internal class ConsulQueryNodeSource : INodeSource
    {
        private DeploymentIdentifier DeploymentIdentifier { get; }
        private int _stopped;
        private readonly object _initLocker = new object();        

        private CancellationTokenSource ShutdownToken { get; }
        
        private INode[] _nodes = new INode[0];
        private Task _initTask;
        

        private ILog Log { get; }
        private ConsulClient ConsulClient { get; }
        private IDateTime DateTime { get; }
        private Func<ConsulConfig> GetConfig { get; }
        private Task LoopingTask { get; set; }
        private string DataCenter { get; }
        private Exception Error { get; set; }
        private DateTime ErrorTime { get; set; }

        public ConsulQueryNodeSource(
            DeploymentIdentifier deploymentIdentifier, 
            ILog log, 
            ConsulClient consulClient, 
            IEnvironmentVariableProvider environmentVariableProvider, 
            IDateTime dateTime, 
            Func<ConsulConfig> getConfig)
        {
            DeploymentIdentifier = deploymentIdentifier;
            Log = log;
            ConsulClient = consulClient;
            DateTime = dateTime;
            GetConfig = getConfig;
            DataCenter = environmentVariableProvider.DataCenter;
            ShutdownToken = new CancellationTokenSource();
        }

        public Task Init()
        {
            lock (_initLocker)
            {
                if (_initTask == null)
                {
                    _initTask = LoadNodes();
                    LoopingTask = Task.Run(LoadNodesLoop);
                }
            }
            return _initTask;
        }

        private async Task LoadNodesLoop()
        {
            try
            {
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
        private async Task LoadNodes()
        {
            if (Error != null)
                await DateTime.DelayUntil(ErrorTime + GetConfig().ErrorRetryInterval, ShutdownToken.Token).ConfigureAwait(false);

            var response = await ConsulClient.GetNodesByQuery(DeploymentIdentifier, ShutdownToken.Token).ConfigureAwait(false);

            if (response.IsUndeployed == true)
            {
                WasUndeployed = true;
                _nodes = new INode[0];
            }
            else if (response.Error != null)
            {
                ErrorResult(response);
            }
            else
            {
                _nodes = response.Result;
                if (_nodes.Length == 0)
                    ErrorResult(response, "No endpoints were specified in Consul for the requested service and service's active version.");
            }
        }

        private void ErrorResult<T>(ConsulResponse<T> response, string errorMessage = null)
        {
            EnvironmentException error = response.Error ?? new EnvironmentException(errorMessage);

            if (error.InnerException is TaskCanceledException == false)
            {
                Log.Error("Error calling Consul", exception: response.Error, unencryptedTags: new
                {
                    serviceName = DeploymentIdentifier,
                    consulAddress = response.ConsulAddress,
                    commandPath = response.CommandPath,
                    responseCode = response.StatusCode,
                    content = response.ResponseContent
                });
            }

            Error = error;
            ErrorTime = DateTime.UtcNow;
        }

        public INode[] GetNodes()
        {
            if (_nodes.Length == 0 && Error != null)
            {
                if (Error.StackTrace == null)
                    throw Error;

                ExceptionDispatchInfo.Capture(Error).Throw();
            }

            return _nodes;
        }

        public bool WasUndeployed { get; private set; } = false;

        public bool SupportsMultipleEnvironments => true;

        public void Shutdown()
        {
            if (Interlocked.Increment(ref _stopped) != 1)
                return;

            ShutdownToken?.Cancel();
            ShutdownToken?.Dispose();
        }
    }
}