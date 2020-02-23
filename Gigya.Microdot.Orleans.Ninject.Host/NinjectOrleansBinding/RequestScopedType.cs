using System;
using System.Collections.Generic;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// We are managing the RequestScopedType out side of ninject 
    /// </summary>
    internal class RequestScopedType: IRequestScopedType
    {
        private readonly HashSet<Type> _isRequestScope = new HashSet<Type>();

        public void Register(Type service)
        {
            _isRequestScope.Add(service);
        }

        public bool Contains(Type service)
        {
            return _isRequestScope.Contains(service);
        }
    }

    internal interface IRequestScopedType
    {
         bool Contains(Type service);
    }
}