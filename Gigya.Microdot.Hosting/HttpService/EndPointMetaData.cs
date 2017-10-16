using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting.HttpService
{
    public class EndPointMetaData
    {
        public EndPointMetaData(ServiceMethod method)
        {
            MethodSensitivity = GetSensitivity(method.ServiceInterfaceMethod);

            var parametersSensitivity = ImmutableDictionary.CreateBuilder<string, Sensitivity?>();

            foreach (var pram in method.ServiceInterfaceMethod.GetParameters())
            {
                parametersSensitivity.Add(pram.Name, GetSensitivity(pram));
            }

            ParametersSensitivity = parametersSensitivity.ToImmutable();
        }

        private Sensitivity? GetSensitivity(ICustomAttributeProvider t)
        {

            var attribute = t.GetCustomAttributes(typeof(SensitiveAttribute),true).FirstOrDefault();
            if (attribute is SensitiveAttribute sensitiveAttribute)
            {
                if (sensitiveAttribute.DoNotLog)
                    return Sensitivity.NoLog;
                return Sensitivity.Encrypted;

            }

            attribute = t.GetCustomAttributes(typeof(NonSensitiveAttribute), true).FirstOrDefault();
            if (attribute is NonSensitiveAttribute)
            {
                return Sensitivity.Unencrypted;
            }
            return null;
        }

        public ImmutableDictionary<string, Sensitivity?> ParametersSensitivity { get; }

        public Sensitivity? MethodSensitivity { get; }

    }

}