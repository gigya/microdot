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
using System.Reflection;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.System_Reflection.DispatchProxy;

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


        private MethodCachingPolicyConfig GetConfig(string methodName)
        {
            GetDiscoveryConfig().Services.TryGetValue(ServiceName, out ServiceDiscoveryConfig config);
            return config?.CachingPolicy?.Methods?[methodName] ?? CachingPolicyConfig.Default;
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
            var config = GetConfig(GetMethodNameForCachingPolicy(targetMethod, args));
            bool useCache = config.Enabled == true && IsMethodCached(targetMethod, args);

            if (useCache)
                return Memoizer.Memoize(DataSource, targetMethod, args, new CacheItemPolicyEx(config));
            else
                return targetMethod.Invoke(DataSource, args);
        }
    }
}
