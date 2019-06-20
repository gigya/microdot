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
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Orleans.Hosting
{
    /// <summary>Provides information about services in this silo.</summary>
    public class ClusterIdentity
    {
        /// <summary>
        /// Provides the ServiceId this orleans cluster is running as.
        /// ServiceId's are intended to be long lived Id values for a particular service which will remain constant 
        /// even if the service is started / redeployed multiple times during its operations life.
        /// </summary>
        public Guid ServiceId { get; }

        /// <summary>
        /// Provides the SiloDeploymentId for this orleans cluster.
        /// </summary>
        public string DeploymentId { get; }


        /// <summary>
        /// Performs discovery of services in the silo and populates the class' static members with information about them.
        /// </summary>
        public ClusterIdentity(ILog log, IEnvironment environment, CurrentApplicationInfo appInfo)
        {

            string dc = environment.Zone;
            string env = environment.DeploymentEnvironment;

            var serviceIdSourceString = string.Join("_", dc, env, appInfo.Name, environment.InstanceName);
            ServiceId = Guid.Parse(serviceIdSourceString.GetHashCode().ToString("X32"));

            DeploymentId = serviceIdSourceString + "_" + appInfo.Version;

            log.Info(_ => _("Orleans Cluster Identity Information (see tags)", unencryptedTags: new { OrleansDeploymentId = DeploymentId, OrleansServiceId = ServiceId, serviceIdSourceString }));
        }
    }
}