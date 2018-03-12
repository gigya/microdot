using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.HttpService;

namespace Gigya.Microdot.Hosting
{
    public class CacheMetadata
    {
        // Instead of HttpServiceRequest should be a base class
        private readonly Dictionary<Type, IList<Func<HttpServiceRequest, object>>> _cacheMetaData;

        public CacheMetadata()
        {
            _cacheMetaData = new Dictionary<Type, IList<Func<HttpServiceRequest, object>>>();
        }



        public bool Add<TType>(TType instance) where TType : class
        {
            var type = typeof(TType);

            if (_cacheMetaData.ContainsKey(type) == false)
            {
                _cacheMetaData[type] = new List<Func<HttpServiceRequest, object>>();

                foreach (var property in ReflectionMetadataExtension.GetProperties(instance))
                {
                    _cacheMetaData[type].Add((Func<HttpServiceRequest, object>)property.Invokation);
                }
            }

            return true;
        }

    }


    public class ReflectionInfo
    {
        public string PropertyName { get; set; }

        public object Value { get; set; }
    }


    public class ReflectionMetadataInfo<TType> where TType :class 
    {
        public string PropertyName { get; set; }
        public Func<TType, object> Invokation { get; set; }
    }


    public static class ReflectionMetadataExtension
    {

        public static ReflectionMetadataInfo<TType> GetProperty<TType>(PropertyInfo propertyInfo) 
            where TType : class
        {

            MethodInfo getterMethodInfo = propertyInfo.GetGetMethod();
            ParameterExpression entity = Expression.Parameter(typeof(TType));
            MethodCallExpression getterCall = Expression.Call(entity, getterMethodInfo);

            UnaryExpression castToObject = Expression.Convert(getterCall, typeof(object));
            LambdaExpression lambda = Expression.Lambda(castToObject, entity);


            return new ReflectionMetadataInfo<TType>
            {
                PropertyName = propertyInfo.Name,
                Invokation = (Func<TType, object>)lambda.Compile()
            };
        }


        

        public static IEnumerable<ReflectionMetadataInfo<TType>> GetProperties<TType>(TType instance)
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
