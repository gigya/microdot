using System;
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Orleans.Ninject.Host.Validators
{
    public class LogFieldAttributeValidator : IValidator
    {
        private readonly IServiceInterfaceMapper _serviceInterfaceMapper;

        public LogFieldAttributeValidator(IServiceInterfaceMapper serviceInterfaceMapper)
        {
            _serviceInterfaceMapper = serviceInterfaceMapper;
        }


        public void Validate()
        {
            foreach (var serviceInterface in _serviceInterfaceMapper.ServiceInterfaceTypes)
            {
                foreach (var method in serviceInterface.GetMethods())
                {
                    LogFieldAppliedOnlyOnClass(serviceInterface, method);
                }
            }
        }

        private void LogFieldAppliedOnlyOnClass(Type serviceInterface, MethodInfo method)
        {
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.GetCustomAttribute(typeof(LogFieldsAttribute)) != null)
                {
                    if (parameter.ParameterType.IsClass == false)
                    {
                        throw new ProgrammaticException($"[LogField] should be applied only on a class type ({parameter.Name}) in method ({method.Name}) on serviceInterface ({serviceInterface.Name})");

                    }
                }
            }
        }
    }
}