using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Hosting
{
    public interface ICacheMetadata
    {
        void Register<TType>() where TType : class;
        IEnumerable<Param> Resolve<TType>(TType instance) where TType : class;
    }

    public class CacheMetadata : ICacheMetadata
    {
        private readonly ConcurrentDictionary<Type, IList<object>> _cache;

        public CacheMetadata()
        {
            _cache = new ConcurrentDictionary<Type, IList<object>>();
        }

        public void Register<TType>() where TType : class
        {
            var type = typeof(TType);
            if (_cache.ContainsKey(type) == false)
            {
                var list = new List<object>();
                _cache[type] = list;

                var properties = ReflectionMetadataExtension.GetProperties<TType>();

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
                Invokation = (Func<TType, object>)lambda.Compile(),
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
