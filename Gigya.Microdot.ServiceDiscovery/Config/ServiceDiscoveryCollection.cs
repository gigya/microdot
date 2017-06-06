﻿#region Copyright 
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

using System.Collections.Generic;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    public class ServiceDiscoveryCollection : ConfigCollection<ServiceDiscoveryConfig>
    {
        private readonly PortAllocationConfig _portAllocationConfig;

        public ServiceDiscoveryCollection(IDictionary<string, ServiceDiscoveryConfig> source, ServiceDiscoveryConfig defaultItem, PortAllocationConfig portAllocationConfig): base(source, defaultItem)
        {
            _portAllocationConfig = portAllocationConfig;
        }

        protected override ServiceDiscoveryConfig ApplyDefaults(ServiceDiscoveryConfig item)
        {
            item.ReloadInterval = item.ReloadInterval ?? DefaultItem.ReloadInterval;
            item.DelayMultiplier = item.DelayMultiplier ?? DefaultItem.DelayMultiplier;
            item.FirstAttemptDelaySeconds = item.FirstAttemptDelaySeconds ?? DefaultItem.FirstAttemptDelaySeconds;
            item.MaxAttemptDelaySeconds = item.MaxAttemptDelaySeconds ?? DefaultItem.MaxAttemptDelaySeconds;
            item.RequestTimeout = item.RequestTimeout ?? DefaultItem.RequestTimeout;
            item.Scope = item.Scope ?? DefaultItem.Scope;
            item.Source = item.Source ?? DefaultItem.Source;

            if (_portAllocationConfig.IsSlotMode && item.DefaultSlotNumber.HasValue)
                item.DefaultPort = _portAllocationConfig.GetPort(item.DefaultSlotNumber, PortOffsets.Http);

            return item;
        }
    }
}
