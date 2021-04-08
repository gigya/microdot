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
using System.Collections.Concurrent;
using System.Reflection;
using Gigya.Common.Contracts.Attributes;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.System_Reflection.DispatchProxy;
using Gigya.ServiceContract.HttpService;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    public class CachingProxyProvider<TInterface> : ICachingProxyProvider<TInterface>
    {
        /// <summary>
        /// The instance of the transparent proxy used to access the data source with caching.
        /// </summary>
        /// <remarks>
        /// This is a thread-safe instance.
        /// </remarks>
        public TInterface Proxy { get; }

        /// <summary>
        /// The instance of the actual data source, used when the data is not present in the cache.
        /// </summary>
        public TInterface DataSource { get; }


        private IMemoizer Memoizer { get; }
        private IMetadataProvider MetadataProvider { get; }
        private ILog Log { get; }
        private IDateTime DateTime { get; }
        private Func<DiscoveryConfig> GetDiscoveryConfig { get; }
        private string ServiceName { get; }
        private ConcurrentDictionary<string /* method name */, (MethodCachingPolicyConfig methodConfig, MethodCachingPolicyConfig effMethodConfig)> 
            CachingConfigPerMethod = new ConcurrentDictionary<string, (MethodCachingPolicyConfig, MethodCachingPolicyConfig)>();


        public CachingProxyProvider(TInterface dataSource, IMemoizer memoizer, IMetadataProvider metadataProvider, Func<DiscoveryConfig> getDiscoveryConfig, ILog log, IDateTime dateTime, string serviceName)
        {
            DataSource = dataSource;
            Memoizer = memoizer;
            MetadataProvider = metadataProvider;
            GetDiscoveryConfig = getDiscoveryConfig;
            Log = log;
            DateTime = dateTime;

            Proxy = DispatchProxy.Create<TInterface, DelegatingDispatchProxy>();
            ((DelegatingDispatchProxy)(object)Proxy).InvokeDelegate = Invoke;
            ServiceName = serviceName ?? typeof(TInterface).GetServiceName();
        }


        private MethodCachingPolicyConfig GetConfig(MethodInfo targetMethod, string methodName)
        {
            GetDiscoveryConfig().Services.TryGetValue(ServiceName, out ServiceDiscoveryConfig discoveryConfig);
            var methodConfig = discoveryConfig?.CachingPolicy?.Methods?[methodName] ?? CachingPolicyConfig.Default;

            if (CachingConfigPerMethod.TryGetValue(methodName, out var cachedMethodConfigTuple) &&
                ReferenceEquals(methodConfig, cachedMethodConfigTuple.methodConfig))
            {
                return cachedMethodConfigTuple.effMethodConfig;
            }
            else
            {
                var effMethodConfig = new MethodCachingPolicyConfig();
                MethodCachingPolicyConfig.Merge(methodConfig, effMethodConfig);

                // TODO: fix below; currently the ServiceDiscoveryConfig merges configs like so: hard-coded defaults --> per-service --> per method
                // In this method we want to merge like this: hard-coded defaults --> per-service --> cached attribute --> per method
                // ...so we probably need access to configs before merges
                //MethodCachingPolicyConfig.Merge(MetadataProvider.GetCachedAttribute(targetMethod), config);
                //US #136496

                // For methods returning Revocable<> responses, we assume they issue manual cache revokes. If the caching settings do not
                // define explicit RefreshMode and ExpirationBehavior, then for Revocable<> methods we don't use refreshes and use a sliding
                // expiration. For non-Revocable<> we do use refreshes and a fixed expiration.

                bool isRevocable = false;

                try
                {
                    var taskResultType = MetadataProvider.GetMethodTaskResultType(targetMethod);
                    isRevocable = taskResultType != null && taskResultType.IsGenericType && taskResultType.GetGenericTypeDefinition() == typeof(Revocable<>);
                }
                catch (Exception e)
                {
                    Log.Error("Error retrieving result type", exception: e);
                }

                if (effMethodConfig.RefreshMode == 0)
                    if (isRevocable)
                        effMethodConfig.RefreshMode = RefreshMode.UseRefreshes; //TODO: change to RefreshMode.UseRefreshesWhenDisconnectedFromCacheRevokesBus after disconnect from bus feature is developed
                    else effMethodConfig.RefreshMode = RefreshMode.UseRefreshes;

                if (effMethodConfig.ExpirationBehavior == 0)
                    if (isRevocable)
                        effMethodConfig.ExpirationBehavior = ExpirationBehavior.DoNotExtendExpirationWhenReadFromCache; //TODO: change to ExpirationBehavior.ExtendExpirationWhenReadFromCache after disconnect from bus feature is developed
                    else effMethodConfig.ExpirationBehavior = ExpirationBehavior.DoNotExtendExpirationWhenReadFromCache;

                //Note: In case we want to add config validations (like we have in CacheAttribute), we can do it here and use Func<string, AggregatingHealthStatus> getAggregatedHealthCheck
                //If validation failed, we wont update the cache, and preserve the error in CachingConfigPerMethod entry value
                //HealthCheck func will set 'Bad' state in case an error exist in any entry. Otherwise it will set 'Good'
                //The error will be cleaned (after config fix), either by a call made to GetConfig with specific methodName
                //Or (if no call to the specific methodName was made), by a timer that cleans old errors from CachingConfigPerMethod

                // Add to cache and return
                CachingConfigPerMethod[methodName] = (methodConfig, effMethodConfig);
                return effMethodConfig;
            }
        }


        protected virtual string GetMethodNameForCachingPolicy(MethodInfo targetMethod, object[] args)
        {
            return targetMethod.Name;
        }

        protected virtual bool IsMethodCached(MethodInfo targetMethod, object[] args)
        {
            return MetadataProvider.IsCached(targetMethod);
        }


        private object Invoke(MethodInfo targetMethod, object[] args)
        {
            var config = GetConfig(targetMethod, GetMethodNameForCachingPolicy(targetMethod, args));
            bool useCache = config.Enabled == true && IsMethodCached(targetMethod, args);

            if (useCache)
                return Memoizer.Memoize(DataSource, targetMethod, args, config);
            else
                return targetMethod.Invoke(DataSource, args);
        }
    }
}
