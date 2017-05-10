using System;
using System.Collections.Generic;
using System.Linq;

using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;

using Ninject.Extensions.Conventions;
using Ninject.Syntax;

namespace Gigya.Microdot.Ninject
{
    public static class MicrodotConventions
    {
        public static void BindClassesAsSingleton(this IBindingRoot bindingRoot, IList<Type> nonSingletonBaseTypes = null, params Type[] assemblies)
        {
            var list = new List<Type>
            {
                typeof(IConfigObject),
                typeof(IEvent)
            };

            if (nonSingletonBaseTypes != null)
            {
                list.AddRange(nonSingletonBaseTypes);
            }

            bindingRoot.Bind(x =>
                {
                    x.FromAssemblyContaining(assemblies)
                    .SelectAllClasses()
                    .Where(t => list.All(nonSingletonType => nonSingletonType.IsAssignableFrom(t) == false))
                    .BindToSelf()
                    .Configure(c => c.InSingletonScope());
                });
        }

        
        public static void BindInterfacesAsSingleton(this IBindingRoot bindingRoot, IList<Type> nonSingletonBaseTypes = null, params Type[] assemblies)
        {
            var list = new List<Type>
            {
                typeof(IConfigObject),
                typeof(IEvent)
            };

            if (nonSingletonBaseTypes != null)
            {
                list.AddRange(nonSingletonBaseTypes);
            }

            bindingRoot.Bind(x =>
                {
                    x.FromAssemblyContaining(assemblies)
                    .SelectAllClasses()
                    .Where(t => list.All(nonSingletonType => nonSingletonType.IsAssignableFrom(t) == false))
                    .BindAllInterfaces()
                    .Configure(c => c.InSingletonScope());
                });
        }
    }
}