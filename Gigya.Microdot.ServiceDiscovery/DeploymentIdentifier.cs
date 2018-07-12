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
namespace Gigya.Microdot.ServiceDiscovery
{
    public class DeploymentIdentifier
    {
        public string DeploymentEnvironment { get; }
        public string ServiceName { get; }

        public bool IsEnvironmentSpecific => string.IsNullOrEmpty(DeploymentEnvironment)==false;

        public DeploymentIdentifier(string serviceName, string deploymentEnvironment)
        {
            DeploymentEnvironment = deploymentEnvironment?.ToLower();
            ServiceName = serviceName;
        }

        public DeploymentIdentifier(string serviceName): this(serviceName, null)
        { }

        public override string ToString()
        {
            if (IsEnvironmentSpecific)
                return $"{ServiceName}-{DeploymentEnvironment}";
            else
                return ServiceName;
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is DeploymentIdentifier other)
            {
                if (IsEnvironmentSpecific && other.IsEnvironmentSpecific)
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
                if (IsEnvironmentSpecific)
                    return ((ServiceName?.GetHashCode() ?? 0) * 397) ^ (DeploymentEnvironment.GetHashCode());
                else
                    return ServiceName?.GetHashCode() ?? 0;
            }
        }
    }
}