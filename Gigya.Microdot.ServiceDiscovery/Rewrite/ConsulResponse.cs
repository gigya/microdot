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

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ConsulResponse<TResonse>
    {
        public bool? IsUndeployed { get; set; }
        public EnvironmentException Error { get; set; }
        public string ConsulAddress { get; set; }
        public string CommandPath { get; set; }
        public string ResponseContent { get; set; }
        public TResonse ResponseObject { get; set; }
        public DateTime ResponseDateTime { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public ulong? ModifyIndex { get; set; }

        public ConsulResponse<T> SetResult<T>(T result)
        {
            return new ConsulResponse<T>
            {
                IsUndeployed = IsUndeployed,
                Error = Error,
                CommandPath = CommandPath,
                ConsulAddress = ConsulAddress,
                ModifyIndex = ModifyIndex,
                ResponseContent = ResponseContent,
                ResponseDateTime = ResponseDateTime,
                StatusCode = StatusCode,
                ResponseObject = result
            };
        }

        public EnvironmentException ConsulUnreachable(Exception innerException)
        {
            return new EnvironmentException("Consul was unreachable.",
                innerException,
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                });
        }

        public EnvironmentException ConsulResponseCodeNotOk()
        {
            return new EnvironmentException("Consul returned a failure response (not 200 OK).",
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                    { "responseContent", ResponseContent },
                    { "responseCode", StatusCode?.ToString() }
                });
        }

        public EnvironmentException UnparsableConsulResponse(Exception innerException)
        {
            return new EnvironmentException("Error deserializing Consul response.",
                innerException,
                unencrypted: new Tags
                {
                    { "consulAddress", ConsulAddress },
                    { "commandPath", CommandPath },
                    { "responseContent", ResponseContent },
                    { "expectedResponseType", typeof(TResonse).Name }
                });
        }
    }
}