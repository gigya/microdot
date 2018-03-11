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
    public class CacheMetaData
    {
        // Instead of HttpServiceRequest should be a base class
        private readonly Dictionary<Type, IList<Func<HttpServiceRequest, object>>> _cacheMetaData;

        public CacheMetaData()
        {
            _cacheMetaData = new Dictionary<Type, IList<Func<HttpServiceRequest, object>>>();
        }



        public bool Add<TType>(TType instance) where TType : class
        {
            var type = typeof(TType);

            if (_cacheMetaData.ContainsKey(type) == false)
            {
                _cacheMetaData[type] = new List<Func<HttpServiceRequest, object>>();

                foreach (var property in ReflectionMetaDataExtension.GetProperties(instance))
                {
                    _cacheMetaData[type].Add((Func<HttpServiceRequest, object>)property);
                }
            }

            return true;
        }

    }



    public static class ReflectionMetaDataExtension
    {
        public static IEnumerable<Func<TType, object>> GetProperties<TType>(TType instance)
        {
            var type = typeof(TType);

            var properties = type.GetProperties().Where(p => p.CanRead);

            foreach (var propertyInfo in properties)
            {
                MethodInfo getterMethodInfo = propertyInfo.GetGetMethod();
                ParameterExpression entity = Expression.Parameter(typeof(TType));
                MethodCallExpression getterCall = Expression.Call(entity, getterMethodInfo);

                UnaryExpression castToObject = Expression.Convert(getterCall, typeof(object));
                LambdaExpression lambda = Expression.Lambda(castToObject, entity);

                var functionThatGetsValue = (Func<TType, object>)lambda.Compile();

                //if (type)
                //var result = functionThatGetsValue(instance);

                yield return functionThatGetsValue;
            }
        }
    }
}
