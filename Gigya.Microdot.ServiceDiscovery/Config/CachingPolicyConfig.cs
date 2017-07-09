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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// Caching Configuration for specific service. Used by CachingProxy.
    /// </summary>
    [Serializable]
    public class CachingPolicyConfig: MethodCachingPolicyConfig
    {
        internal MethodCachingPolicyConfig DefaultItem { get; private set; }

        /// <summary>
        /// The discovery configuration for the various services.
        /// </summary>
        public IImmutableDictionary<string, MethodCachingPolicyConfig> Methods { get; set; }  // <method name, caching policy params>        

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            DefaultItem = new MethodCachingPolicyConfig
            {
                Enabled = Enabled ?? Default.Enabled,
                ExpirationTime = ExpirationTime ?? Default.ExpirationTime,
                FailedRefreshDelay = FailedRefreshDelay ?? Default.FailedRefreshDelay,
                RefreshTime = RefreshTime ?? Default.RefreshTime
            };
        
            var methods = (IDictionary<string, MethodCachingPolicyConfig>)Methods ?? new Dictionary<string, MethodCachingPolicyConfig>();

            Methods = new CachingPolicyCollection(methods, DefaultItem);
        }

        public static MethodCachingPolicyConfig Default =>
                new MethodCachingPolicyConfig
                {
                    Enabled = true,
                    RefreshTime = TimeSpan.FromMinutes(1),
                    ExpirationTime = TimeSpan.FromHours(6),
                    FailedRefreshDelay = TimeSpan.FromSeconds(1)
                };

    }
}