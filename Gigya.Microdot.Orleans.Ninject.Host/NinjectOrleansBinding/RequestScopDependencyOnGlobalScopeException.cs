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
using static Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding.CacheItem;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    public class RequestScopDependencyOnGlobalScopeException : Exception
    {
        public RequestScopDependencyOnGlobalScopeException()
        {
        }

        public RequestScopDependencyOnGlobalScopeException(string message) : base(message)
        {
        }

        public RequestScopDependencyOnGlobalScopeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected RequestScopDependencyOnGlobalScopeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}


