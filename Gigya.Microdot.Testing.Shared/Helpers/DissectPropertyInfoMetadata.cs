using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Testing.Shared.Helpers
{
    public static class DissectPropertyInfoMetadata
    {
        public static IEnumerable<(PropertyInfo PropertyInfo, Sensitivity Sensitivity)> DissectPropertis<TType>(TType instance, Sensitivity defualtSensitivity = Sensitivity.Sensitive) where TType : class
        {
            foreach (var propertyInfo in instance.GetType().GetProperties())
            {
                var sensitivity = ExtractSensitivityFromPropertyInfo(propertyInfo) ?? defualtSensitivity;
                yield return (propertyInfo, sensitivity);
            }
        }

        public static Sensitivity? ExtractSensitivityFromPropertyInfo(PropertyInfo propertyInfo)
        {
            var attribute = propertyInfo.GetCustomAttributes()
                .FirstOrDefault(x => x is SensitiveAttribute || x is NonSensitiveAttribute);

            if (attribute != null)
            {
                if (attribute is SensitiveAttribute sensitiveAttibute)
                {
                    if (sensitiveAttibute.Secretive)
                    {
                        return Sensitivity.Secretive;
                    }

                    return Sensitivity.Sensitive;
                }

                return Sensitivity.NonSensitive;
            }

            return null;
        }
    }
}
