using System.Collections.Generic;
using System.Reflection;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    internal static class DissectPropertyInfoMetadata
    {

        internal static IEnumerable<(PropertyInfo propertyInfo, Sensitivity sensitivity)> DissectPropertis<TType>(TType instance) where TType : class
        {
            foreach (var propertyInfo in instance.GetType().GetProperties())
            {
                var sensitivity = ReflectionMetadataExtension.ExtractSensitivity(propertyInfo) ?? Sensitivity.Sensitive;
                yield return (propertyInfo, sensitivity);

            }

        }


    }
}
