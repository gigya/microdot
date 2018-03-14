using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting
{
    public class ReflectionMetadataInfo<TType> where TType : class
    {
        public string PropertyName { get; set; }
        public Func<TType, object> ValueExtractor { get; set; }

        public Sensitivity ? Sensitivity { get; set; }
    }


    public static class ReflectionMetadataExtension
    {


        public static IEnumerable<ReflectionMetadataInfo<TType>> ExtracMetadata<TType>()
            where TType : class
        {
            var type = typeof(TType);
            var getters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead)
                .Select(x => new
                {
                    Getter = x.GetGetMethod(),
                    PropertyName = x.Name,
                    Sensitivity = ExtractSensitivity(x) // nullable
                });


            var metadatas = new List<ReflectionMetadataInfo<TType>>();


            foreach (var getter in getters)
            {
                ParameterExpression entity = Expression.Parameter(typeof(TType));
                MethodCallExpression getterCall = Expression.Call(entity, getter.Getter);

                UnaryExpression castToObject = Expression.Convert(getterCall, typeof(object));
                LambdaExpression lambda = Expression.Lambda(castToObject, entity);

                metadatas.Add(new ReflectionMetadataInfo<TType>
                {
                    PropertyName = getter.PropertyName,
                    ValueExtractor = (Func<TType, object>)lambda.Compile(),
                    Sensitivity = getter.Sensitivity
                });
            }

            return metadatas;
        }

        //--------------------------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------------------------

        private static Sensitivity ? ExtractSensitivity(PropertyInfo propertyInfo)
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


        //public static ReflectionMetadataInfo<TType> GetProperty<TType>(PropertyInfo propertyInfo)
        //    where TType : class
        //{

        //    MethodInfo getterMethodInfo = propertyInfo.GetGetMethod();
        //    ParameterExpression entity = Expression.Parameter(typeof(TType));
        //    MethodCallExpression getterCall = Expression.Call(entity, getterMethodInfo);

        //    UnaryExpression castToObject = Expression.Convert(getterCall, typeof(object));
        //    LambdaExpression lambda = Expression.Lambda(castToObject, entity);


        //    return new ReflectionMetadataInfo<TType>
        //    {
        //        PropertyName = propertyInfo.Name,
        //        ValueExtractor = (Func<TType, object>)lambda.Compile(),
        //        Sensitivity = Sensitivity.NonSensitive
        //    };
        //}

        //public static IEnumerable<ReflectionMetadataInfo<TType>> GetProperties<TType>()
        //    where TType : class
        //{
        //    var type = typeof(TType);

        //    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead);


        //    foreach (var propertyInfo in properties)
        //    {
        //        yield return GetProperty<TType>(propertyInfo);
        //    }
        //}
    }
}