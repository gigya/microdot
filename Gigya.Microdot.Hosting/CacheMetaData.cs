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
        IEnumerable<Param> ParseIntoParams<TType>(TType instance) where TType : class;
    }

    public class CacheMetadata : ICacheMetadata //todo: rename cahcemetadata 
    {
        private readonly ConcurrentDictionary<Type, object[]> _cache = new ConcurrentDictionary<Type, object[]>();

        private void Register<TType>() where TType : class
        {
            var type = typeof(TType);

            _cache.GetOrAdd(type, x => ReflectionMetadataExtension.ExtracMetadata<TType>().Cast<object>().ToArray());
        }


        private IEnumerable<Param> Resolve<TType>(TType instance) where TType : class
        {
            foreach (var item in _cache[typeof(TType)])
            {
                var workinItem = (ReflectionMetadataInfo<TType>)item;
                var value = workinItem.ValueExtractor(instance);

                //todo: return with object value
                yield return new Param
                {
                    Name = workinItem.PropertyName,
                    Value = value.ToString(), // serialize same as in publishEvent
                    Sensitivity = workinItem.Sensitivity
                };
            }
        }

        public IEnumerable<Param> ParseIntoParams<TType>(TType instance) where TType : class
        {

            Register<TType>();
            return Resolve(instance);
        }



    }




}
