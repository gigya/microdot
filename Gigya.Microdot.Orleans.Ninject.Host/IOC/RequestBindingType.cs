using System;
using System.Collections.Generic;

namespace Gigya.Microdot.Orleans.Ninject.Host.IOC
{
    // Mark all request scope 
    public class RequestBindingType
    {
        private readonly Dictionary<Type, bool> _isRequestScope = new Dictionary<Type, bool>();

        public void Register(Type service)
        {
            _isRequestScope.Add(service, true);
        }

        public bool IsRequest(Type service)
        {
            return _isRequestScope.ContainsKey(service);
        }
    }
}