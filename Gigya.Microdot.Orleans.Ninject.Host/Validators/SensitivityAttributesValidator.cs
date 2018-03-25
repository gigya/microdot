using System;
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Orleans.Ninject.Host.Validators
{
    public class SensitivityAttributesValidator : IValidator
    {
        private readonly IServiceInterfaceMapper _serviceInterfaceMapper;


        public SensitivityAttributesValidator(IServiceInterfaceMapper serviceInterfaceMapper)
        {
            _serviceInterfaceMapper = serviceInterfaceMapper;
        }


        public void Validate()
        {
            foreach (var serviceInterface in _serviceInterfaceMapper.ServiceInterfaceTypes)
            {
                foreach (var method in serviceInterface.GetMethods())
                {
                    SensitiveAndNonSensitiveAppliedOnTheSameMethod(serviceInterface, method);
                    SensitiveAndNonSensitiveAppliedOnTheSameParameter(serviceInterface, method);
                    //LogFieldAndNonSensitiveAppliedOnTheSameParameter(serviceInterface, method);

                }
            }
        }

        //private void LogFieldAndNonSensitiveAppliedOnTheSameParameter(Type serviceInterface, MethodInfo method)
        //{
        //    foreach (var parameter in method.GetParameters())
        //    {
        //        if (parameter.GetCustomAttribute(typeof(SensitiveAttribute)) != null && parameter.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
        //        {
        //            throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same parameter ({parameter.Name}) in method ({method.Name}) on serviceInterface ({serviceInterface.Name})");
        //        }
        //    }
        //}

        private void SensitiveAndNonSensitiveAppliedOnTheSameParameter(Type serviceInterface, MethodInfo method)
        {
            foreach (var parameter in method.GetParameters())
            {
                if (parameter.GetCustomAttribute(typeof(SensitiveAttribute)) != null && parameter.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                {
                    throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same parameter ({parameter.Name}) in method ({method.Name}) on serviceInterface ({serviceInterface.Name})");
                }
            }
        }


        private void SensitiveAndNonSensitiveAppliedOnTheSameMethod(Type serviceInterface, MethodInfo method)
        {
            if (method.GetCustomAttribute(typeof(SensitiveAttribute)) != null && method.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
            {
                throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same method ({method.Name}) on serviceInterface ({serviceInterface.Name})");
            }
        }
    }
}