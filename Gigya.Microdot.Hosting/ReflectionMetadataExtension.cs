using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting
{
    public class ReflectionMetadataInfo<TType> where TType : class
    {
        public string PropertyName { get; set; }
        public Func<TType, object> ValueExtractor { get; set; }

        public bool IsSensitive { get; set; }
    }
    public static class ReflectionMetadataExtension
    {

        public static ReflectionMetadataInfo<TType> GetProperty<TType>(PropertyInfo propertyInfo)
            where TType : class
        {

            MethodInfo getterMethodInfo = propertyInfo.GetGetMethod();
            bool isCryptic = Attribute.IsDefined(getterMethodInfo, typeof(SensitiveAttribute));

            ParameterExpression entity = Expression.Parameter(typeof(TType));
            MethodCallExpression getterCall = Expression.Call(entity, getterMethodInfo);

            UnaryExpression castToObject = Expression.Convert(getterCall, typeof(object));
            LambdaExpression lambda = Expression.Lambda(castToObject, entity);


            return new ReflectionMetadataInfo<TType>
            {
                PropertyName = propertyInfo.Name,
                ValueExtractor = (Func<TType, object>)lambda.Compile(),
                IsSensitive = isCryptic
            };
        }

        public static IEnumerable<ReflectionMetadataInfo<TType>> GetProperties<TType>()
            where TType : class
        {
            var type = typeof(TType);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead);

            foreach (var propertyInfo in properties)
            {
                yield return GetProperty<TType>(propertyInfo);
            }
        }
    }
}