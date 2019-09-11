using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Gigya.Microdot.Testing.Shared.Helpers
{
    public static class DissectPropertyInfoMetadata
    {
        public static Sensitivity? ExtractSensitivityFromPropertyInfo(MemberInfo memberInfo)
        {
            var attribute = memberInfo.GetCustomAttributes()
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

                // If we got here, we definitely found a NonSensitiveAttribute
                return Sensitivity.NonSensitive;
            }

            return null;
        }

        public static IEnumerable<(object Value, MemberTypes MemberType, string Name, Sensitivity? Sensitivity, bool WithException, MemberInfo Member)> GetMemberWithSensitivity<TInstance>(TInstance instance, Sensitivity defualtSensitivity = Sensitivity.Sensitive) where TInstance : class
        {
            var members = GetMembers(instance).ToArray();

            foreach (var member in members)
            {
                var sensitivity = ExtractSensitivityFromPropertyInfo(member.Member);
                yield return (member.Value, member.MemberType, member.Name, sensitivity, member.WithException, member.Member);
            }
        }

        public static IEnumerable<(object Value, MemberTypes MemberType, string Name, bool WithException, MemberInfo Member)> GetMembers<TInstance>(TInstance instance) where TInstance : class
        {
            var members = instance.GetType().FindMembers(MemberTypes.Property | MemberTypes.Field, BindingFlags.Public | BindingFlags.Instance, null, null)
                .Where(x => (x is FieldInfo) || ((x is PropertyInfo propertyInfo) && propertyInfo.CanRead));

            foreach (var member in members)
            {
                var withException = false;
                if (member.MemberType == MemberTypes.Property)
                {
                    object value = null;

                    try
                    {
                        value = ((PropertyInfo)member).GetValue(instance);
                    }
                    catch (Exception)
                    {
                        withException = true;
                    }

                    yield return (value, MemberTypes.Property, member.Name, withException, member);
                }
                else
                {
                    if (member.MemberType == MemberTypes.Field)
                        yield return (((FieldInfo)member).GetValue(instance), MemberTypes.Field, member.Name, withException, member);
                }
            }
        }
    }
}