using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Gigya.Microdot.Interfaces.HttpService;

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

            var target = new InvocationTarget(method);
            string cacheKey = $"{target}#{GetArgumentHash(args)}";
            
            return Cache.GetOrAdd(cacheKey, () => (Task)method.Invoke(dataSource, args), taskResultType, policy, target.TypeName, target.MethodName);
        }


        private string GetArgumentHash(object[] args)
        {
            var stream = new MemoryStream();
            using (ComputeArgumentHash.NewContext())
            using (var writer = new StreamWriter(stream) { AutoFlush = true })
            using (SHA1 sha = new SHA1CryptoServiceProvider())
            {
                JsonSerializer.Create().Serialize(writer, args);
                stream.Seek(0, SeekOrigin.Begin);
                return Convert.ToBase64String(sha.ComputeHash(stream));
            }
        }
    }
}