using System;
using System.Linq;
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.ServiceContract.Attributes;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Hosting.Validators
{
    public class LogFieldAttributeValidator : IValidator
    {
        private readonly IServiceInterfaceMapper _serviceInterfaceMapper;
        private readonly Type[] _typesPrecludedFromLogFieldAttributeUsage;

        public LogFieldAttributeValidator(IServiceInterfaceMapper serviceInterfaceMapper)
        {
            _serviceInterfaceMapper = serviceInterfaceMapper;

            _typesPrecludedFromLogFieldAttributeUsage = new[]
            {
                typeof(string), typeof(JToken), typeof(Type)
            };
        }

        public void Validate()
        {
            foreach (var serviceInterface in _serviceInterfaceMapper.ServiceInterfaceTypes)
            {
                foreach (var method in serviceInterface.GetMethods())
                {
                    LogFieldAppliedOnlyOnClassParameters(serviceInterface, method);
                }
            }
        }

        private void LogFieldAppliedOnlyOnClassParameters(Type serviceInterface, MethodInfo method)
        {
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.GetCustomAttribute(typeof(LogFieldsAttribute)) != null)
                {
                    if (parameter.ParameterType.IsClass == false || _typesPrecludedFromLogFieldAttributeUsage.Any(x => x == parameter.ParameterType))
                    {
                        throw new ProgrammaticException($"LogFieldAttribute cannot be applied to parameter '{parameter.Name}' of method '{method.Name}' on '{serviceInterface.Name}'. It can only be applied to reference types, except the following types: ${string.Join(", ", _typesPrecludedFromLogFieldAttributeUsage.Select(x => x.Name))}");
                    }
                }
            }
        }
    }
}