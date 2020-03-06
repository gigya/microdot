using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Ninject;
using Ninject.Activation;
using Ninject.Activation.Caching;
using Ninject.Activation.Strategies;
using Ninject.Injection;
using Ninject.Modules;
using Ninject.Parameters;
using Ninject.Planning;
using Ninject.Planning.Bindings;
using Ninject.Planning.Bindings.Resolvers;
using Ninject.Planning.Strategies;
using Ninject.Planning.Targets;
using Ninject.Selection;
using Ninject.Selection.Heuristics;
using Ninject.Syntax;
using static Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding.ScopeCache;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{

    /// <summary>
    /// Thrown when you ask Ikeranl/ fun<T> / IResoltionRoot to resolve scope depedncy
    /// </summary>
    public class GlobalScopeNotSupportFromNinject : Exception
    {
        public GlobalScopeNotSupportFromNinject()
        {
        }

        public GlobalScopeNotSupportFromNinject(string message) : base(message)
        {
        }

        public GlobalScopeNotSupportFromNinject(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected GlobalScopeNotSupportFromNinject(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}


