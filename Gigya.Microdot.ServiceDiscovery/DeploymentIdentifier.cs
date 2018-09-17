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
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class DeploymentIdentifier
    {
        /// <summary>The environment (e.g. "prod", "st1") of the service, if it's deployed in a specific environment (and
        /// not per the whole zone). Null otherwise.</summary>
        public string DeploymentEnvironment { get; } = null;

        /// <summary>The name of the service (e.g. "AccountsService").</summary>
        public string ServiceName { get; }

        /// <summary>The zone of the service (e.g. "us1a").</summary>
        public string Zone { get; }

        /// <summary>
        /// Whether this deployment identifier points to a service deployed for a specific environment, or is it deployed for all environments
        /// </summary>
        public bool IsEnvironmentSpecific => DeploymentEnvironment != null;

        /// <summary>
        /// Create a new identifier for a service which is deployed on current datacenter 
        /// </summary>
        public DeploymentIdentifier(string serviceName, string deploymentEnvironment, IEnvironment environment) : this(serviceName, deploymentEnvironment, environment.Zone) { }

        /// <summary>
        /// Create a new identifier for a service which is deployed on a different datacenter
        /// </summary>
        public DeploymentIdentifier(string serviceName, string deploymentEnvironment, string zone)
        {
            DeploymentEnvironment = deploymentEnvironment?.ToLower();
            if (serviceName == null || zone == null)
                throw new ArgumentNullException();
            ServiceName = serviceName;
            Zone = zone;
        }

        public override string ToString()
        {
            var serviceAndEnv = IsEnvironmentSpecific ? $"{ServiceName}-{DeploymentEnvironment}" : ServiceName;

            return $"{serviceAndEnv} ({Zone})";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is DeploymentIdentifier other)
            {
                if (Zone != other.Zone)
                    return false;

                return DeploymentEnvironment == other.DeploymentEnvironment && ServiceName == other.ServiceName;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ServiceName.GetHashCode();
                hashCode = (hashCode * 397) ^ (DeploymentEnvironment?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ Zone.GetHashCode();
                return hashCode;
            }
        }

    }
}