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
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class DiscoverySourceLoader : IDiscoverySourceLoader
    {
        public DiscoverySourceLoader(Func<string, ServiceDiscoveryConfig, ConfigDiscoverySource>  getConfigDiscoverySource,
                                     Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulDiscoverySource> getConsulDiscoverySource,
                                     Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulQueryDiscoverySource> getConsulQueryDiscoverySourc)
        {
            _getConfigDiscoverySource = getConfigDiscoverySource;
            _getConsulDiscoverySource = getConsulDiscoverySource;
            _getConsulQueryDiscoverySource = getConsulQueryDiscoverySourc;
        }

        private readonly Func<string, ServiceDiscoveryConfig, ConfigDiscoverySource> _getConfigDiscoverySource;
        private readonly Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulDiscoverySource> _getConsulDiscoverySource;
        private readonly Func<ServiceDeployment, ServiceDiscoveryConfig, ConsulQueryDiscoverySource> _getConsulQueryDiscoverySource;

        public ServiceDiscoverySourceBase GetDiscoverySource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoverySettings)
        {
            switch (serviceDiscoverySettings.Source)
            {
                case DiscoverySource.Config:
                    return _getConfigDiscoverySource(serviceDeployment.ServiceName, serviceDiscoverySettings);
                case DiscoverySource.Consul:
                    return _getConsulDiscoverySource(serviceDeployment, serviceDiscoverySettings);
                case DiscoverySource.ConsulQuery:
                    return _getConsulQueryDiscoverySource(serviceDeployment, serviceDiscoverySettings);
                case DiscoverySource.Local:
                    return new LocalDiscoverySource(serviceDeployment.ServiceName);
            }

            throw new ConfigurationException($"Source '{serviceDiscoverySettings.Source}' is not supported by any configuration.");
        }
    }

    public interface IDiscoverySourceLoader
    {
        ServiceDiscoverySourceBase GetDiscoverySource(ServiceDeployment serviceDeployment, ServiceDiscoveryConfig serviceDiscoverySettings);
    }
}