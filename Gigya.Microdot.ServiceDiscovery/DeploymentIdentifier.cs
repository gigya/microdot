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

using System.Security.Policy;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class DeploymentIdentifier
    {
        public string DeploymentEnvironment { get; }
        public string ServiceName { get; }
        public string DataCenter { get; }

        public bool IsEnvironmentSpecific => string.IsNullOrEmpty(DeploymentEnvironment)==false;

        /// <summary>
        /// Create a new identifier for a service which is deployed on current datacenter 
        /// </summary>
        public DeploymentIdentifier(string serviceName, string deploymentEnvironment, IEnvironment environment)
        {
            DeploymentEnvironment = deploymentEnvironment?.ToLower();
            ServiceName = serviceName;
            DataCenter = environment.DataCenter;
        }

        /// <summary>
        /// Create a new identifier for a service which is deployed on a different datacenter
        /// </summary>
        public DeploymentIdentifier(string serviceName, string deploymentEnvironment, string dataCenter)
        {
            DeploymentEnvironment = deploymentEnvironment?.ToLower();
            ServiceName = serviceName;
            DataCenter = dataCenter;
        }

        public override string ToString()
        {
            var serviceAndEnv = IsEnvironmentSpecific ? $"{ServiceName}-{DeploymentEnvironment}" : ServiceName;

            return $"{serviceAndEnv} ({DataCenter})";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is DeploymentIdentifier other)
            {
                if (DataCenter != other.DataCenter)
                    return false;

                if (IsEnvironmentSpecific || other.IsEnvironmentSpecific)
                    return DeploymentEnvironment == other.DeploymentEnvironment && ServiceName == other.ServiceName;
                else
                    return ServiceName == other.ServiceName;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ServiceName!=null? ServiceName.GetHashCode() : 0;
                if (IsEnvironmentSpecific)
                    hashCode = (hashCode * 397) ^ DeploymentEnvironment.GetHashCode();
                hashCode = (hashCode * 397) ^ (DataCenter != null ? DataCenter.GetHashCode() : 0);
                return hashCode;
            }
        }

    }
}