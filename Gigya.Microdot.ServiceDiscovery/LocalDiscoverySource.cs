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
using System.Net;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.ServiceDiscovery
{
    /// <summary>
    /// Returns the current computer as the sole node in the list of endpoints. Never changes.
    /// </summary>
    public class LocalDiscoverySource : ServiceDiscoverySourceBase
    {
        public override string SourceName => "Local";

        public LocalDiscoverySource(DeploymentIdentifier deploymentIdentifier) : base($"{CurrentApplicationInfo.HostName}-{deploymentIdentifier.ServiceName}")
        {
            Result = new EndPointsResult{EndPoints  = new[] { new EndPoint { HostName = CurrentApplicationInfo.HostName }}} ;
        }

        public override Exception AllEndpointsUnreachable(EndPointsResult endPointsResult, Exception lastException, string lastExceptionEndPoint, string unreachableHosts)
        {
            return new ServiceUnreachableException("Service source is configured to 'Local' Discovery mode, but is not reachable on local machine. See tags for more details.",
                lastException,
                unencrypted: new Tags
                {
                    {"unreachableHosts", unreachableHosts},
                    {"configPath", $"Discovery.{Deployment}.Source"},
                    {"requestedService", Deployment},
                    {"innerExceptionIsForEndPoint", lastExceptionEndPoint}
                });
        }
        
        public override bool IsServiceDeploymentDefined => true;
    }

}