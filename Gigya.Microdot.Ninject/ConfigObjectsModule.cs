using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Dataflow;

using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces.Configuration;

using Ninject;
using Ninject.Activation;
using Ninject.Infrastructure;
using Ninject.Modules;
using Ninject.Planning.Bindings;
using Ninject.Planning.Bindings.Resolvers;

namespace Gigya.Microdot.Ninject
{
    public class ConfigObjectsModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.BindPerKey<Type, ConfigObjectCreator>();
            Kernel.Components.Add<IBindingResolver, ConfigObjectsBindingResolver>();
        }
    }

    public sealed class ConfigObjectsBindingResolver : IBindingResolver
    {
        public INinjectSettings Settings { get; set; }

        public IEnumerable<IBinding> Resolve(Multimap<Type, IBinding> bindings, Type service)
        {
            ConfigObjectProvider provider;

            if (IsConfigObject(service))
                provider = new ConfigObjectProvider(false);
            else if (service.IsGenericType && service.GetGenericTypeDefinition() == typeof(ISourceBlock<>) &&
                    IsConfigObject(service.GetGenericArguments().Single()) && bindings[service].Any() == false)
                provider = new ConfigObjectProvider(true);
            else
                return Enumerable.Empty<IBinding>();


            return new[]
            {
                new Binding(service)
                {
                    ProviderCallback = ctx => provider,
                    ScopeCallback = StandardScopeCallbacks.Transient
                }
            };
        }


        private bool IsConfigObject(Type type)
        {
            return type.IsClass && type.IsAbstract == false && typeof(IConfigObject).IsAssignableFrom(type);
        }


        public void Dispose() {  }
    }


    public class ConfigObjectProvider : IProvider
    {
        private bool IsBroadcast { get; }
        public Type Type => IsBroadcast ? typeof(ISourceBlock<IConfigObject>) : typeof(IConfigObject);
        private ConfigObjectCreator Creator { get; set; }


        public ConfigObjectProvider(bool isBroadcast)
        {
            IsBroadcast = isBroadcast;
        }


        public object Create(IContext context)
        {
            if (Creator == null)
            {
                var getCreator = context.Kernel.Get<Func<Type, ConfigObjectCreator>>();
                var service = context.Request.Service;
                Creator = getCreator(IsBroadcast ? service.GetGenericArguments().Single() : service);
            }

            return IsBroadcast ? Creator.ChangeNotifications : Creator.GetLatest();
        }
    }
}
