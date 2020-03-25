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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// Config object read from <configuration.Discovery/> XML entry. Will be recreated when config changes.
    /// </summary>
    [Serializable]
    [ConfigurationRoot("Discovery", RootStrategy.ReplaceClassNameWithPath)]
    public class DiscoveryConfig : IConfigObject
    {        
        internal ServiceDiscoveryConfig DefaultItem { get; private set; }

        /// <summary>
        /// Scope where this service is installed.
        /// Some services are installed for current environment only (itg1, prod, etc.)
        /// Other services are installed for entire data-center (e.g. Kafka, Flume, etc.)
        /// </summary>
        public ServiceScope Scope { get; set; } = ServiceScope.Environment;

        /// <summary>
        /// The time-out trying to send requests to the service.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// Time period to keep monitoring a deployed service after it was no longer requested
        /// </summary>
        public TimeSpan MonitoringLifetime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// When we lose connection to some endpoint, we wait this delay till we start trying to reconnect.
        /// </summary>
        [Obsolete("To be deleted after discovery refactoring")]
        public double FirstAttemptDelaySeconds { get; set; } = 0.001;

        /// <summary>
        /// When retrying to reconnect to an endpoint, we use exponential backoff (e.g. 1,2,4,8ms, etc). Once that
        /// backoff reaches this value, it won't increase any more.
        /// </summary>
        [Obsolete("To be deleted after discovery refactoring")]
        public double MaxAttemptDelaySeconds { get; set; } = 10;

        /// <summary>
        /// The factor of the exponential backoff when retrying connections to endpoints.
        /// </summary>
        [Obsolete("To be deleted after discovery refactoring")]
        public double DelayMultiplier { get; set; } = 2;

        public bool UseHttpsOverride { get; set; } = false;

        /// <summary>
        /// The discovery mode to use, e.g. whether to use DNS resolving, Consul, etc.
        /// </summary>
        public string Source { get; set; } = ConsulDiscoverySource.Name;

        /// <summary>
        /// The discovery configuration for the various services.
        /// </summary>
        public IImmutableDictionary<string, ServiceDiscoveryConfig> Services { get; set; }  // <service name, discovery params>

        [Required]
        public PortAllocationConfig PortAllocation { get; set; } = new PortAllocationConfig();

        public bool EnvironmentFallbackEnabled { get; set; } = false;

        public string EnvironmentFallbackTarget { get; set; } 
        
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            DefaultItem = new ServiceDiscoveryConfig
            {
                DelayMultiplier = DelayMultiplier,
                FirstAttemptDelaySeconds = FirstAttemptDelaySeconds,
                MaxAttemptDelaySeconds = MaxAttemptDelaySeconds,
                RequestTimeout = RequestTimeout,
                Scope = Scope,
                Source = Source
            };

            var services = (IDictionary<string, ServiceDiscoveryConfig>)Services?? new Dictionary<string, ServiceDiscoveryConfig>();
            
            Services = new ServiceDiscoveryCollection(services, DefaultItem, PortAllocation);
        }
    }
}