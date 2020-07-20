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
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.System_Reflection.DispatchProxy;

namespace Gigya.Microdot.ServiceProxy
{
    public class ServiceProxyProvider<TInterface>: IServiceProxyProvider<TInterface>
    {

        /// <summary>
        /// The instance of the transparent proxy client used to access the remote service that implements <i>TInterface</i>.
        /// </summary>
        /// <remarks>
        /// This is a thread-safe instance.
        /// </remarks>
        public TInterface Client { get; }

        /// <summary>
        /// Gets the name of the remote service. This defaults to the friendly name that was specified in the
        /// <see cref="HttpServiceAttribute"/> decorating <i>TInterface</i>. If none were specified, the interface name
        /// is used.
        /// </summary>
        public string ServiceName => InnerProvider.ServiceName;
        
        /// <summary>
        /// Gets or sets the port used to access the remote service. This defaults to the port that was specified in the
        /// <see cref="HttpServiceAttribute"/> decorating <i>TInterface</i>, unless overridden by configuration.
        /// </summary>
        public int? DefaultPort { get => InnerProvider.DefaultPort; set => InnerProvider.DefaultPort = value; }

        /// <summary>
        /// Specifies a delegate that can be used to change a request in a user-defined way before it is sent over the
        /// network.
        /// </summary>
        public Action<HttpServiceRequest> PrepareRequest { get => InnerProvider.PrepareRequest; set => InnerProvider.PrepareRequest = value; }

        internal IServiceProxyProvider InnerProvider { get; }

        // ReSharper disable once MemberCanBePrivate.Global
        public ServiceProxyProvider(Func<string, IServiceProxyProvider> serviceProxyFactory)
        {
            var attribute = (HttpServiceAttribute)Attribute.GetCustomAttribute(typeof(TInterface), typeof(HttpServiceAttribute));

            if (attribute == null)
                throw new ProgrammaticException("The specified service interface type is not decorated with HttpServiceAttribute.", unencrypted: new Tags { { "interfaceName", typeof(TInterface).Name } });

            InnerProvider = serviceProxyFactory(typeof(TInterface).GetServiceName());
            InnerProvider.ServiceInterfaceRequiresHttps = attribute.UseHttps;

            if (InnerProvider.DefaultPort==null)
            {
                InnerProvider.DefaultPort = attribute.BasePort + (int)PortOffsets.Http;                
            }

            Client = DispatchProxy.Create<TInterface, DelegatingDispatchProxy>();
            ((DelegatingDispatchProxy)(object)Client).InvokeDelegate = Invoke;
        }


        /// <summary>
        /// Sets the length of time to wait for a HTTP request before aborting the request.
        /// </summary>
        /// <param name="timeout">The maximum length of time to wait.</param>
        public void SetHttpTimeout(TimeSpan timeout) => InnerProvider.SetHttpTimeout(timeout);



        private object Invoke(MethodInfo targetMethod, object[] args)
        {
            var resultReturnType = targetMethod.ReturnType.GetGenericArguments().SingleOrDefault() ?? typeof(object);

            var request = new HttpServiceRequest(targetMethod, args);

            return TaskConverter.ToStronglyTypedTask(InnerProvider.Invoke(request, resultReturnType), resultReturnType);
        }

    }
}