using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.ServiceDiscovery.HostManagement
{
    public class RemoteHost: EndPoint, IEndPointHandle
    {
        internal Exception LastException { get; private set; }
        
        private object Lock { get; }
        protected RemoteHostPool HostPool { get; }
        private ILog Log { get; }
        private int FailureCount { get; set; }
        private bool IsMonitoring { get; set; }

     

        internal RemoteHost(string hostName, RemoteHostPool remoteHostPool, object sharedLock, int? port=null)
        {
			if (hostName == null)
				throw new ArgumentNullException(nameof(hostName));
            if (remoteHostPool == null) 
				throw new ArgumentNullException(nameof(remoteHostPool));
	        if (sharedLock == null) 
				throw new ArgumentNullException(nameof(sharedLock));

	        Lock = sharedLock;
			HostPool = remoteHostPool;
			HostName = hostName;            
	        FailureCount = 0;
            Log = remoteHostPool.Log;
            Port = port;
        }

        /// <summary>
		/// Reports that an attempt to contact a <see cref="RemoteHost"/> has failed. Based on these reports, the reachability of <see cref="RemoteHost"/> is determined.
        /// </summary>
        /// <param name="ex">Optional. The exception containing the details of the failed attempt.</param>
		/// <returns>True if this <see cref="RemoteHost"/> is still considered reachable despite the failure, or false if it has been marked as unreachable.</returns>
        public virtual bool ReportFailure(Exception ex = null)
        {
            LastException = ex;

            lock (Lock)
            { 
                FailureCount++;

	            var isReachable = FailureCount < 2;

				if (isReachable == false && HostPool.MarkUnreachable(this))
		        {
					Log.Error(_ => _("A remote host has been marked as unreachable due to failing twice. Monitoring " + 
                        "started. See tags and inner exception for details.",
                        exception: ex,
                        unencryptedTags: new
                        {
                            endpoint = HostName,
                            requestedService = HostPool.ServiceDeployment.ServiceName,
                            requestedServiceEnvironment = HostPool.ServiceDeployment.DeploymentEnvironment

                        }));

                    // Task.Run is used here to have the long-running task of monitoring run on the thread pool,
                    // otherwise it might prevent ASP.NET requests from completing or it might be tied to a specific
                    // grain's lifetime, when it is actually a global concern.
                    Task.Run(StartMonitoring);
                }

                return isReachable;
            }
        }

		/// <summary>
		/// Reports that an attempt to contact a <see cref="RemoteHost"/> has succeeded. Based on these reports, the reachability of <see cref="RemoteHost"/> is determined.
		/// </summary>
		public virtual void ReportSuccess()
		{
			lock (Lock)
			{
				FailureCount = 0;
			}
		}

		/// <summary>
		/// Monitors a host that has been marked unreachable by using the parent RemoteHostPool.IsReachableChecker using exponential backoff.
		/// </summary>
	    [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
	    private async Task StartMonitoring()
	    {
		    lock (Lock)
		    {
			    if (IsMonitoring)
				    return;
			    
				IsMonitoring = true;
		    }

		    var config = HostPool.GetConfig();
		    var start = DateTime.UtcNow;
			var nextDelay = TimeSpan.FromSeconds(config.FirstAttemptDelaySeconds.Value);
			var maxDelay = TimeSpan.FromSeconds(config.MaxAttemptDelaySeconds.Value);
		    var attemptCount = 1;

		    while (true)
		    {
				if (IsMonitoring == false)
					return;

			    try
			    {
					attemptCount++;

					if (await HostPool.ReachabilityChecker.Invoke(this).ConfigureAwait(false))
					    break;

			    }
			    catch (Exception ex)
			    {
					Log.Error(_ => _("The supplied reachability checker threw an exception while checking a remote host. See tags and inner exception for details.",
                        exception: ex,
                        unencryptedTags: new
                        {
                            endpoint = HostName,
                            requestedService = HostPool.ServiceDeployment.ServiceName,
                            requestedServiceEnvironment = HostPool.ServiceDeployment.DeploymentEnvironment
                        }));
				}

				if (IsMonitoring == false)
					return;

				lock (Lock)
					FailureCount++;

				Log.Info(_ => _("A remote host is still unreachable, monitoring continues. See tags for details", unencryptedTags: new
				{
					endpoint = HostName,
                    requestedService = HostPool.ServiceDeployment.ServiceName,
                    requestedServiceEnvironment = HostPool.ServiceDeployment.DeploymentEnvironment,
                    attemptCount,
					nextDelay,
					nextAttemptAt = DateTime.UtcNow + nextDelay,
					downtime = DateTime.UtcNow - start
				}));

			    await Task.Delay(nextDelay).ConfigureAwait(false);

				nextDelay = TimeSpan.FromMilliseconds(nextDelay.TotalMilliseconds * config.DelayMultiplier.Value);
				
				if (nextDelay > maxDelay)
					nextDelay = maxDelay;
		    }

		    lock (Lock)
		    {
			    FailureCount = 0;
			    IsMonitoring = false;

		        if (HostPool.MarkReachable(this))
		        {
		            Log.Info(_ => _("A remote host has become reachable. See tags for details.", 
                        unencryptedTags: new
                        {
                            endpoint = HostName,
                            requestedService = HostPool.ServiceDeployment.ServiceName,
                            requestedServiceEnvironment = HostPool.ServiceDeployment.DeploymentEnvironment,
                            attemptCount,
                            downtime = DateTime.UtcNow - start
                        }));
		        }
		    }
	    }

	    internal void StopMonitoring()
	    {
		    IsMonitoring = false;
	    }

	    public override string ToString()
	    {
		    return HostName;
	    }
    }

}