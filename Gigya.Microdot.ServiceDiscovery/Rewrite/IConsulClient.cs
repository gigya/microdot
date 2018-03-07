using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public interface IConsulClient: IDisposable
    {
        Task LoadNodes(ConsulServiceState serviceState);
    }
}