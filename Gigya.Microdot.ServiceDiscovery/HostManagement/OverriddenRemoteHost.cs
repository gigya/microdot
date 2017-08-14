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
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.HostManagement
{
    public class OverriddenRemoteHost : IEndPointHandle
    {
        private string ServiceName { get; }
        public string HostName { private set;get; }
        public int? Port { private set; get; }


        internal OverriddenRemoteHost(string serviceName, string hostName, int? port = null)
        {
            ServiceName = serviceName;
            HostName = hostName;
            Port = port;

        }



        public bool ReportFailure(Exception ex = null)
        {
            throw new MissingHostException("Failed to reach an overridden remote host. Please make sure the " +
                                           "overrides specified are reachable from all services that participate in the request. See inner " +
                                           "exception for details and tags for information on which override caused this issue.",
                ex,
                unencrypted: new Tags
                {
                    { "overriddenServiceName", ServiceName },
                    { "overriddenHostName", HostName },
                    { "overriddenPort", Port?.ToString() }

                });
        }

        public void ReportSuccess()
        {
        }


    }
}
