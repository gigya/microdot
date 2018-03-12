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
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Hosting
{

    public class CacheMetadata
    {
        private readonly Dictionary<Type, IList<object>> _cache = new Dictionary<Type, IList<object>>();

        public CacheMetadata()
        {

        }

        public void Add<TType>(TType instance) where TType : class
        {
            var type = typeof(TType);
            if (_cache.ContainsKey(type) == false)
            {
                var list = new List<object>();
                _cache[type] = list;

                var properties = ReflectionMetadataExtension.GetProperties(instance);

                foreach (var property in properties)
                {
                    list.Add(property);
                }
            }
        }

        public IEnumerable<Param> Resolve<TType>(TType instance) where TType : class
        {
            var list = new List<Param>();

            foreach (var item in _cache[typeof(TType)])
            {
                var workinItem = (ReflectionMetadataInfo<TType>)item;
                var value = workinItem.Invokation(instance);

                list.Add(new Param
                {
                    Name = workinItem.PropertyName,
                    Value = value.ToString()
                });
            }

            return list;
        }


    }


    public class ReflectionInfo
    {
        public string PropertyName { get; set; }

        public object Value { get; set; }
    }

    class ReflectionMetadataInput
    {
        public object Metadata { get; set; }
    }

    public class ReflectionMetadataInfo<TType> where TType : class
    {
        public string PropertyName { get; set; }
        public Func<TType, object> Invokation { get; set; }

        //Sensitive
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
