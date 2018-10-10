#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public class HealthEndpoint : ICustomEndpoint
    {
        private readonly IServiceDrainListener _drainListener;
        private IServiceEndPointDefinition ServiceEndPointDefinition { get; }
        private IServiceInterfaceMapper ServiceInterfaceMapper { get; }
        private IActivator Activator { get; }
        private const int WebServerIsDown = 521;

        public HealthEndpoint(IServiceEndPointDefinition serviceEndPointDefinition, IServiceInterfaceMapper serviceInterfaceMapper, IActivator activator,IServiceDrainListener drainListener)
        {
            _drainListener = drainListener;
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

                if (_drainListener.Token.IsCancellationRequested)
                {
                    await writeResponse($"Begin service drain before shutdown.",(HttpStatusCode)WebServerIsDown ).ConfigureAwait(false);
                }

                if (serviceType == null)
                    throw new RequestException("Invalid service name");

                if (ServiceInterfaceMapper.HealthStatusServiceType == null || serviceType.IsAssignableFrom(ServiceInterfaceMapper.HealthStatusServiceType) == false)
                {
                    await writeResponse(string.Empty).ConfigureAwait(false);
                }
                else
                {
                    var healthStatusResult = await CheckServiceHealth().ConfigureAwait(false);

                    var status = healthStatusResult.IsHealthy ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;
                    var json = healthStatusResult.Message;
                    await writeResponse(json, status).ConfigureAwait(false);
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
            var endedTask = await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10)), invocationTask).ConfigureAwait(false);

            if (endedTask != invocationTask)
            {
                return new HealthStatusResult("Health status check took too long.", false);
            }

            var invocationResult = await invocationTask.ConfigureAwait(false);

            return invocationResult.Result as HealthStatusResult;
        }
    }
}