using System;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Check if the specified node is reachable. 
    /// Task should finish successfully if service is reachable, or throw a descriptive exception if it is not
    /// </summary>    
    public delegate Task ReachabilityCheck(Node node, CancellationToken cancellationToken);
}