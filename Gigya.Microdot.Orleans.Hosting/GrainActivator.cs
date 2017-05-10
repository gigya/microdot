using System;
using System.Collections.Concurrent;

using Gigya.Microdot.Hosting.HttpService;

using Orleans;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class GrainActivator : AbstractServiceActivator
    {
        private Lazy<IGrainFactory> Factory { get; }
        private ConcurrentDictionary<Type, IGrain> GrainCache { get; }

        public GrainActivator(Lazy<IGrainFactory> factory)
        {
            Factory = factory;
            GrainCache = new ConcurrentDictionary<Type, IGrain>();
        }

        protected override object GetInvokeTarget(ServiceMethod serviceMethod)
        {
            return GrainCache.GetOrAdd(serviceMethod.GrainInterfaceType, t => GetGrain(serviceMethod.GrainInterfaceType));
        }

        private IGrain GetGrain(Type grainInterfaceType)
        {
            var getGrainMethod = typeof(IGrainFactory).GetMethod("GetGrain", new[] { typeof(long), typeof(string) })
                .MakeGenericMethod(grainInterfaceType);

            return (IGrain)getGrainMethod.Invoke(Factory.Value, new object[] { 0, null });
        }
    }
}