using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{

    public interface INodeSourceFactory
    {
        string Type {get;}
       
        Task<INodeSource> TryCreateNodeSource(DeploymentIdentifier di);
    }
}