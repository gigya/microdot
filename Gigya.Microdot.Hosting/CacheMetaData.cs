using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Hosting
{
    public interface ICacheMetadata
    {
        void Register<TType>() where TType : class;
        IEnumerable<Param> Resolve<TType>(TType instance) where TType : class;
        IEnumerable<Param> ParseIntoParams<TType>(TType dictionaryEntryValue) where TType : class;
    }

    public class CacheMetadata : ICacheMetadata
    {
        private readonly ConcurrentDictionary<Type, object[]> _cache;
        private readonly ConcurrentDictionary<Type, Func<object, object[]>> _cachess;

        public CacheMetadata()
        {
            _cache = new ConcurrentDictionary<Type, object[]>();
        }

        public void Register<TType>() where TType : class
        {
            var type = typeof(TType);
            if (_cache.ContainsKey(type) == false)
            {
                _cache[type] = ReflectionMetadataExtension.ExtracMetadata<TType>().Cast<object>().ToArray();
            }

        }


        public IEnumerable<Param> Resolve<TType>(TType instance) where TType : class
        {
            var list = new List<Param>();

            foreach (var item in _cache[typeof(TType)])
            {
                var workinItem = (ReflectionMetadataInfo<TType>)item;
                var value = workinItem.ValueExtractor(instance);

                list.Add(new Param
                {
                    Name = workinItem.PropertyName,
                    Value = value.ToString(),
                    Sensitivity =workinItem.Sensitivity
                });
            }

            return list;
        }

        public IEnumerable<Param> ParseIntoParams<TType>(TType instance) where TType : class
        {

            Register<TType>();
            return Resolve(instance);
        }



    }




}
