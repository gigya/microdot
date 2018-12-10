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
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Utils;
using Metrics;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    /// <summary>
    /// Memorizes asynchronous methods results.
    /// </summary>
    public class AsyncMemoizer : IMemoizer
    {
        private AsyncCache Cache { get; }
        private MetadataProvider MetadataProvider { get; }
        private MetricsContext Metrics { get; }
        private Timer ComputeArgumentHash { get; }


        public AsyncMemoizer(AsyncCache cache, MetadataProvider metadataProvider, MetricsContext metrics)
        {
            Cache = cache;
            MetadataProvider = metadataProvider;
            Metrics = metrics;
            ComputeArgumentHash = Metrics.Timer("ComputeArgumentHash", Unit.Calls);
        }


        public object Memoize(object dataSource, MethodInfo method, object[] args, CacheItemPolicyEx policy)
        {
            if (dataSource == null) throw new ArgumentNullException(nameof(dataSource));
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (args == null) throw new ArgumentNullException(nameof(args));

            var taskResultType = MetadataProvider.GetMethodTaskResultType(method);

            if (taskResultType == null)
                throw new ArgumentException("The specified method doesn't return Task<T> and therefore cannot be memoized", nameof(method));

            var target = new InvocationTarget(method, method.GetParameters());
            string cacheKey = $"{target}#{GetArgumentHash(args)}";
            
            return Cache.GetOrAdd(cacheKey, () => (Task)method.Invoke(dataSource, args), taskResultType, policy, target.MethodName, string.Join(",", args), new []{target.TypeName, target.MethodName});
        }


        private string GetArgumentHash(object[] args)
        {
            using (ComputeArgumentHash.NewContext())
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            using (SHA1 sha = new SHA1CryptoServiceProvider())
            {
                JsonSerializer.Create().Serialize(writer, args);                
                stream.Seek(0, SeekOrigin.Begin);
                return Convert.ToBase64String(sha.ComputeHash(stream));
            }
        }

        public void Dispose()
        {
            Cache.TryDispose();
            Metrics.TryDispose();
        }
    }
}