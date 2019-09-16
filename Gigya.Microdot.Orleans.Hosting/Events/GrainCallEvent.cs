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
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Orleans.Hosting.Events
{
    public class GrainCallEvent : StatsEvent
    {
        public override string EventType => EventConsts.GrainReqType;

        [EventField(EventConsts.targetType)]
        public string TargetType { get; set; }

        [EventField(EventConsts.targetMethod)]
        public string TargetMethod { get; set; }
        
        [EventField(EventConsts.grainPrimaryKey)]
        public string GrainID { get; set; }

        [EventField(EventConsts.siloAddress)]
        public string SiloAddress { get; set; }
        
        [EventField(EventConsts.siloDeploymentId)]
        public string SiloDeploymentId { get; set; }
    }
}
