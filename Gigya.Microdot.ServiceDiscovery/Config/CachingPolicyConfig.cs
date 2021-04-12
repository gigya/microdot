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
using Gigya.Common.Contracts.Attributes;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    /// <summary>
    /// Caching Configuration for specific service. Used by CachingProxy.
    /// </summary>
    [Serializable]
    public class CachingPolicyConfig: MethodCachingPolicyConfig
    {
        /// <summary>
        /// The discovery configuration for the various services.
        /// </summary>
        public IImmutableDictionary<string, MethodCachingPolicyConfig> Methods { get; set; }  // <method name, caching policy params>        

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            
        }

        public static readonly MethodCachingPolicyConfig Default = new MethodCachingPolicyConfig
        {
            // Note! RefreshMode & ExpirationBehavior defaults, depends on whether it is a 'Revocable' method
            // So their defaults will be set in later phase of configuration resolution

            Enabled                                       = true,
            RefreshTime                                   = TimeSpan.FromMinutes(1),
            ExpirationTime                                = TimeSpan.FromHours(6),
            FailedRefreshDelay                            = TimeSpan.FromSeconds(1),
            ResponseKindsToCache                          = ResponseKinds.NonNullResponse | ResponseKinds.NullResponse,
            ResponseKindsToIgnore                         = ResponseKinds.EnvironmentException | ResponseKinds.OtherExceptions | ResponseKinds.RequestException | ResponseKinds.TimeoutException,
            RequestGroupingBehavior                       = RequestGroupingBehavior.Enabled,
            RefreshBehavior                               = RefreshBehavior.UseOldAndFetchNewValueInBackground,
            RevokedResponseBehavior                       = RevokedResponseBehavior.TryFetchNewValueNextTimeOrUseOld, // Behavior change
            CacheResponsesWhenSupressedBehavior           = CacheResponsesWhenSupressedBehavior.Enabled,
            NotIgnoredResponseBehavior                    = NotIgnoredResponseBehavior.KeepCachedResponse
        };

    }
}