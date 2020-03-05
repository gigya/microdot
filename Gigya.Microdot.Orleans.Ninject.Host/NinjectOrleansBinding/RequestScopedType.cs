using System;
using System.Collections.Generic;
using System.Threading;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// We are managing the RequestScopedType out side of ninject 
    /// </summary>
    internal class RequestScopedType : IRequestScopedType
    {
        private HashSet<Type> _isRequestScope = new HashSet<Type>();
        private object _locker = new object();
        public void Register(Type service)
        {
            lock (_locker)
            {
                var isRequestScope = new HashSet<Type>(_isRequestScope);
                isRequestScope.Add(service);
                _isRequestScope = isRequestScope;
            }
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