using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting.HttpService
{
    public class EndPointMetadata
    {
        public EndPointMetadata(ServiceMethod method)
        {
            Initialize(method);
        }

        //--------------------------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------------------------

        public ImmutableDictionary<string, (Sensitivity? Visibility, bool IsLogFieldAttributeExists)> ParamaerAttributes { get; private set; }

        //--------------------------------------------------------------------------------------------------------------------------------------

        public Sensitivity? MethodSensitivity { get; private set; }


        //--------------------------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------------------------

        private void Initialize(ServiceMethod method)
        {

            MethodSensitivity = GetSensitivity(method.ServiceInterfaceMethod);

            var parameterAttributes = ImmutableDictionary.CreateBuilder<string, (Sensitivity? visibility, bool isLogFieldAttributeExists)>();

            foreach (var param in method.ServiceInterfaceMethod.GetParameters())
            {
                var tuple = (Visibility: GetSensitivity(param), IsLogFieldAttributeExists: param.GetCustomAttribute<LogFieldsAttribute>() != null);
                parameterAttributes.Add(param.Name, tuple);
            }

            ParamaerAttributes = parameterAttributes.ToImmutable();
        }

        //--------------------------------------------------------------------------------------------------------------------------------------

        private Sensitivity? GetSensitivity(ICustomAttributeProvider t)
        {

            var attribute = t.GetCustomAttributes(typeof(SensitiveAttribute), true).FirstOrDefault();
            if (attribute is SensitiveAttribute sensitiveAttribute)
            {
                if (sensitiveAttribute.Secretive)
                    return Sensitivity.Secretive;
                return Sensitivity.Sensitive;

            }

            attribute = t.GetCustomAttributes(typeof(NonSensitiveAttribute), true).FirstOrDefault();
            if (attribute is NonSensitiveAttribute)
            {
                return Sensitivity.NonSensitive;
            }
            return null;
        }


        //--------------------------------------------------------------------------------------------------------------------------------------

        
    }

}