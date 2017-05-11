using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;


namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public class HealthEndpoint : ICustomEndpoint
    {
        private IServiceEndPointDefinition ServiceEndPointDefinition { get; }
        private IServiceInterfaceMapper ServiceInterfaceMapper { get; }
        private IActivator Activator { get; }


        public HealthEndpoint(IServiceEndPointDefinition serviceEndPointDefinition, IServiceInterfaceMapper serviceInterfaceMapper, IActivator activator)
        {
            ServiceEndPointDefinition = serviceEndPointDefinition;
            ServiceInterfaceMapper = serviceInterfaceMapper;
            Activator = activator;
        }


        public async Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse)
        {
            if (context.Request.RawUrl.EndsWith(".status"))
            {
                // verify that the service implement IHealthStatus                    
                var serviceName = context.Request.RawUrl.Substring(1, context.Request.RawUrl.LastIndexOf(".", StringComparison.Ordinal) - 1);
                var serviceType = ServiceEndPointDefinition.ServiceNames.FirstOrDefault(o => o.Value == $"I{serviceName}").Key;

                if (serviceType == null)
                    throw new RequestException("Invalid service name");

                if (ServiceInterfaceMapper.HealthStatusServiceType == null || serviceType.IsAssignableFrom(ServiceInterfaceMapper.HealthStatusServiceType) == false)
                {
                    await writeResponse(string.Empty);
                }
                else
                {
                    var healthStatusResult = await CheckServiceHealth();

                    var status = healthStatusResult.IsHealthy ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;
                    var json = healthStatusResult.Message;
                    await writeResponse(json, status);
                }

                return true;
            }

            return false;
        }


        private async Task<HealthStatusResult> CheckServiceHealth()
        {
            var serviceMethod = new ServiceMethod(ServiceInterfaceMapper.HealthStatusServiceType, typeof(IHealthStatus).GetMethod("Status"));

            var invocationTask = Activator.Invoke(serviceMethod, new object[0]);

            // Health check must always complete in less than 10 seconds.
            var endedTask = await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10)), invocationTask);

            if (endedTask != invocationTask)
            {
                return new HealthStatusResult("Health status check took too long.", false);
            }

            var invocationResult = await invocationTask;

            return invocationResult.Result as HealthStatusResult;
        }
    }
}