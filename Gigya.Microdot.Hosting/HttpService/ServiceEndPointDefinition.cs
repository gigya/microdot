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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;

namespace Gigya.Microdot.Hosting.HttpService
{
    /// <summary>
    /// Contains metadata and method resolution generated from the provided <see cref="IServiceInterfaceMapper"/>.
    /// </summary>
    public class ServiceEndPointDefinition : IServiceEndPointDefinition, IMetricsSettings
    {
        public bool UseSecureChannel { get; }

        public int MetricsPort { get; }

        public int HttpPort { get; }

        public int SiloGatewayPort { get; }

        public int SiloNetworkingPort { get; }

        public int SiloNetworkingPortOfPrimaryNode { get; }

        public Dictionary<Type, string> ServiceNames { get; }

        private ConcurrentDictionary<ServiceMethod,  EndPointMetadata> _metadata= new ConcurrentDictionary<ServiceMethod, EndPointMetadata>();

        private readonly ServiceMethodResolver _serviceMethodResolver;


        public ServiceEndPointDefinition(IServiceInterfaceMapper mapper,
            ServiceArguments serviceArguments, Func<DiscoveryConfig> getConfig)
        {
            _serviceMethodResolver = new ServiceMethodResolver(mapper);
            var serviceInterfaces = mapper.ServiceInterfaceTypes.ToArray();

            if (serviceInterfaces.Any() == false)
                throw new ArgumentException("No service interfaces found in the specified assemblies");

            ServiceNames = serviceInterfaces
                .Where(i => i.GetCustomAttribute<HttpServiceAttribute>() != null)
                .ToDictionary(x => x, x => x.Name);

            var interfacePorts = serviceInterfaces.Select(i =>
            {

                var attr = i.GetCustomAttribute<HttpServiceAttribute>();

                return new
                {
                    ServiceInterface = i,
                    BasePort = serviceArguments.BasePortOverride ?? attr.BasePort,
                    BasePortWithoutOverrides = attr.BasePort,
                    attr.UseHttps
                };
            }).ToArray();

            if (interfacePorts.Select(x => x.UseHttps).Distinct().Count() > 1)
            {
                throw new EnvironmentException("Mix of secure and insecure services.");
            }

            var config = getConfig();
            var serviceConfig = config.Services[CurrentApplicationInfo.Name];

            UseSecureChannel = serviceConfig.UseHttpsOverride ?? interfacePorts.First().UseHttps;

            if (config.PortAllocation.IsSlotMode == false && serviceArguments.SlotNumber == null)
            {
                if (interfacePorts.Select(x => x.BasePort).Distinct().Count() > 1)
                {
                    var conflictingPortList = string.Join("\n", interfacePorts.Select(x => $"BasePort {x.BasePort} for {x.ServiceInterface.FullName}"));
                    throw new EnvironmentException("More than one base port was specified for service interfaces:\n" + conflictingPortList);
                }

                var basePort = interfacePorts.First().BasePort;

                HttpPort = basePort + (int)PortOffsets.Http;
                MetricsPort = basePort + (int)PortOffsets.Metrics;
                SiloGatewayPort = basePort + (int)PortOffsets.SiloGateway;
                SiloNetworkingPort = basePort + (int)PortOffsets.SiloNetworking;
                SiloNetworkingPortOfPrimaryNode = interfacePorts.First().BasePortWithoutOverrides + (int)PortOffsets.SiloNetworking;
            }
            else
            {

                if (serviceConfig.DefaultSlotNumber == null)
                    throw new ConfigurationException("Service is configured to run in slot based port but " +
                                                     "DefaultSlotNumber is not set in configuration. " +
                                                     "Either disable this mode via Service.IsSlotMode config value or set it via " +
                                                     $"Discovery.{CurrentApplicationInfo.Name}.DefaultSlotNumber.");                

                int? slotNumber = serviceArguments.SlotNumber ?? serviceConfig.DefaultSlotNumber;

                if (slotNumber == null)
                    throw new ConfigurationException("Service is configured to run in slot based port but SlotNumber " +
                                                     "command-line argument was not specified and DefaultSlotNumber is not set in configuration. " +
                                                     "Either disable this mode via Service.IsSlotMode config value or set it via " +
                                                     $"Discovery.{CurrentApplicationInfo.Name}.DefaultSlotNumber.");

                HttpPort = config.PortAllocation.GetPort(slotNumber, PortOffsets.Http).Value;
                MetricsPort = config.PortAllocation.GetPort(slotNumber, PortOffsets.Metrics).Value;
                SiloGatewayPort = config.PortAllocation.GetPort(slotNumber, PortOffsets.SiloGateway).Value;
                SiloNetworkingPort = config.PortAllocation.GetPort(slotNumber, PortOffsets.SiloNetworking).Value;
                SiloNetworkingPortOfPrimaryNode = config.PortAllocation.GetPort(serviceConfig.DefaultSlotNumber, PortOffsets.SiloNetworking).Value;
            }

            foreach (var method in _serviceMethodResolver.GrainMethods)
            {
                GetMetaData(method);
            }
        }


        public ServiceMethod Resolve(InvocationTarget target)
        {
            return _serviceMethodResolver.Resolve(target);
        }
        public EndPointMetadata GetMetaData(ServiceMethod method)
        {
            return _metadata.GetOrAdd(method, m => new EndPointMetadata(m));

        }


        public ServiceMethod[] GetAll()
        {
            return _serviceMethodResolver.GrainMethods;
        }

    }
}