using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    /// <summary>
    /// A standard endpoint that monitoring services
    /// </summary>    
    public interface IHealthStatus
    {
        /// <summary>
        /// A call to this endpoint initialize a "self diagnostics" process on the service where health indicators can be checked. 
        /// </summary>
        /// <returns>OK by default</returns>
        Task<HealthStatusResult> Status();
    }
}