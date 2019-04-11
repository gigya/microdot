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
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class EndPointsResult
    {
        /// <summary>
        /// Result of endpoints which returned by Consul
        /// </summary>
        public EndPoint[] EndPoints { get; set; } = new EndPoint[0];

        /// <summary>
        /// Log of Request sent to Consul
        /// </summary>
        public string RequestLog { get; set; }

        /// <summary>
        /// Log of Response from Consul
        /// </summary>
        public string ResponseLog { get; set; }

        public DateTime RequestDateTime { get; set; }

        public Exception Error { get; set; }

        public bool IsQueryDefined { get; set; } = true;

        /// <summary>
        /// The version of the service that all traffic should be directed to. 
        /// There may be deployed other versions which are undergoing deployment or maintenance and shouldn't be used.
        /// </summary>
        public string ActiveVersion { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is EndPointsResult other))
                return false;

            return EndPoints.SequenceEqual(other.EndPoints)
                   && IsQueryDefined == other.IsQueryDefined
                   && Error?.Message == other.Error?.Message
                   && ActiveVersion == other.ActiveVersion;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = EndPoints.FirstOrDefault()?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (Error != null ? Error.Message.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsQueryDefined.GetHashCode();
                hashCode = (hashCode * 397) ^ (ActiveVersion != null ? ActiveVersion.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"EndpointsResult: {EndPoints.Length} Endpoints, IsQueryDefined: {IsQueryDefined}, Error: {Error?.Message ?? "null"}";
        }
    }

    public interface IConsulClient: IDisposable
    {
        Task Init();
        EndPointsResult Result { get; }
        ISourceBlock<EndPointsResult> ResultChanged { get; }
        Uri ConsulAddress { get; }
    }



    public class ConsulEndPoint : EndPoint
    {
        /// <summary>
        /// Service version which is installed on this endpoint
        /// </summary>
        public string Version { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is ConsulEndPoint other))
                return false;

            if (Version != other.Version)
                return false;

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (Version != null ? Version.GetHashCode() : 0);
            }
        }
    }

}