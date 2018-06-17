using System;
using System.Linq;
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting.Validators
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
                    if (method.GetCustomAttribute(typeof(SensitiveAttribute)) != null && method.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                    {
                        throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same method ({method.Name}) on serviceInterface ({serviceInterface.Name})");
                    }

                    foreach (var parameter in method.GetParameters())
                    {
                        if (parameter.GetCustomAttribute(typeof(SensitiveAttribute)) != null && parameter.GetCustomAttribute(typeof(NonSensitiveAttribute)) != null)
                        {
                            throw new ProgrammaticException($"[Sensitive] and [NonSensitive] can't both be applied on the same parameter ({parameter.Name}) in method ({method.Name}) on serviceInterface ({serviceInterface.Name})");
                        }

                        if (parameter.ParameterType.IsClass && parameter.ParameterType != typeof(string))
                        {
                            if (parameter.GetCustomAttribute(typeof(LogFieldsAttribute)) == null)
                            {
                                SearchSensitivityAttribute(parameter.ParameterType);
                            }
                        }
                    }
                }
            }
        }

        private void SearchSensitivityAttribute(Type type)
        {
            if (type.IsClass == false || type == typeof(string))
            {
                return;
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (IsSensitivityAttributeExists(property))
                {
                    throw new ProgrammaticException($"[SensitiveAttribute] should not be applied on a Property of an complex parameter withot LogField Attribute");
                }

                SearchSensitivityAttribute(property.PropertyType);
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (IsSensitivityAttributeExists(field))
                {
                    throw new ProgrammaticException($"[SensitiveAttribute] should not be applied on a Field of an complex parameter withot LogField Attribute");
                }

                SearchSensitivityAttribute(field.FieldType);
            }
        }


        private bool IsSensitivityAttributeExists(MemberInfo memberInfo) => Attribute.IsDefined(memberInfo, typeof(SensitiveAttribute));
    }
}