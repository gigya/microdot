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

using System.Diagnostics;
using Gigya.Microdot.Interfaces.Events;

namespace Gigya.Microdot.SharedLogic.Events
{

    public class ClientCallEvent : Event
    {
        public override string EventType => EventConsts.ClientReqType;

        /// <summary>The name of the calling  system (comments/socialize/hades/mongo etc)</summary>
        [EventField(EventConsts.targetService)]
        public virtual string TargetService { get; set; }

        /// <summary>The name of a calling server</summary>    
        [EventField(EventConsts.targetHost)]
        public string TargetHostName { get; set; }

        [EventField(EventConsts.targetPort)]
        public int TargetPort { get; set; }

        /// <summary>Service method called</summary>
        [EventField(EventConsts.targetMethod)]
        public string TargetMethod { get; set; }

        [EventField(EventConsts.targetEnvironment)]
        public string TargetEnvironment { get; set; }

        /// <summary>Stopwatch timestamp when request was sent.</summary>
        [EventField(EventConsts.clnSendTimestamp)]
        public long? RequestStartTimestamp { get; set; }

        /// <summary>Stopwatch timestamp when response was received. Not published, used in <see cref="TotalTimeMS"/>
        /// which is published.</summary>
        public long? ResponseEndTimestamp { get; set; }

        [EventField(EventConsts.protocolMethod)]
        public string ProtocolMethod { get; set; }

        [EventField(EventConsts.protocolParams)]
        public string ProtocolParams { get; set; }

        [EventField(EventConsts.protocolSchema)]

        public string ProtocolSchema { get; set; }

        /// <summary>Total time in milliseconds from sending the request till we got a response.</summary>
        [EventField(EventConsts.statsTotalTime, OmitFromAudit = true)]
        public double? TotalTimeMS => (ResponseEndTimestamp - RequestStartTimestamp) / (Stopwatch.Frequency / 1000.0);

        /// <summary>Time in milliseconds on a server.</summary>
        [EventField(EventConsts.statsServerTime, OmitFromAudit = true)]
        public double? ServerTimeMs { get; set; }

        /// <summary>Total time - ServerTimeMs</summary>
        [EventField(EventConsts.statsNetworkTime, OmitFromAudit = true)]
        public double? NetworkTimeMS => TotalTimeMS - ServerTimeMs;

        [EventField(EventConsts.SuppressCaching)]
        public CacheSuppress? SuppressCaching { get; set; }

        // Debug srv&cln send recv stats
        [EventField(EventConsts.statsRetryCount)]
        public int? StatsRetryCount { get; set; }

        [EventField(EventConsts.above10KmsgLength)]
        public int? Above10KmsgLength { get; set; }

        [EventField(EventConsts.isNewClientCreated)]
        public bool IsNewClientCreated { get; set; }

        [EventField(EventConsts.statsNetworkPostTime)]
        public long StatsNetworkPostTime { get; set; }

        [EventField(EventConsts.postDateTicks)]
        public long PostDateTicks { get; set; }

        [EventField(EventConsts.clientReadResponseTime)]
        public long ClientReadResponseTime { get; set; }

        [EventField(EventConsts.outstandingSentRequests)]
        public long OutstandingSentRequests { get; set; }
        // Debug srv&cln send recv stats

    }
}
