using System;
using System.Collections.Generic;
using System.Linq;

using Gigya.Microdot.ServiceContract.Exceptions;
using Gigya.Microdot.Hosting.HttpService.Endpoints;

namespace Gigya.Microdot.Hosting.HttpService
{
    /// <summary>
    /// Base implementation of IServiceInterfaceMapper
    /// </summary>
    public abstract class ServiceInterfaceMapper : IServiceInterfaceMapper
    {  
        public virtual IEnumerable<Type> ServiceInterfaceTypes { get; protected set; }

        public Type HealthStatusServiceType { get; set; }

        public abstract Type GetGrainInterface(Type serviceInterface);

        /// <summary>
        /// Extract the service implementing IHealthStatus        
        /// </summary>
        /// <exception cref="ProgrammaticException">throw exception with more than one service implemented IHealthStatus</exception>
        /// <param name="types"></param>
        protected void ExtractHealthStatusServiceType(IEnumerable<Type> types)
        {
            var healthStatusGrains = types.Where(i => i.GetInterface(typeof(IHealthStatus).Name) != null).ToArray();

            if (healthStatusGrains.Length > 1)
                throw new ProgrammaticException($"{typeof(IHealthStatus).Name} cannot be assigned to more than one service");

            HealthStatusServiceType = healthStatusGrains.FirstOrDefault();
        }
    }
}