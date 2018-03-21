#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class ReflectionMetadataInfo<TType> where TType : class
    {
        public string PropertyName { get; set; }
        public Func<TType, object> ValueExtractor { get; set; }

        public Sensitivity? Sensitivity { get; set; }
    }


    public interface IPropertiesMetadataPropertiesCache
    {
        IEnumerable<MetadataCacheParam> ParseIntoParams<TType>(TType instance) where TType : class;
    }

    public class MetadataCacheParam
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public Sensitivity? Sensitivity { get; set; }

    }


    public class PropertiesMetadataPropertiesCache : IPropertiesMetadataPropertiesCache
    {
        private readonly ConcurrentDictionary<Type, object[]> _cache = new ConcurrentDictionary<Type, object[]>();

        public IEnumerable<MetadataCacheParam> ParseIntoParams<TType>(TType instance) where TType : class
        {

            Register<TType>();
            return Resolve(instance);
        }

        private void Register<TType>() where TType : class
        {
            var type = typeof(TType);

            _cache.GetOrAdd(type, x => ExtracPropertiesValues<TType>().Cast<object>().ToArray());
        }

        private IEnumerable<MetadataCacheParam> Resolve<TType>(TType instance) where TType : class
        {
            foreach (var item in _cache[typeof(TType)])
            {
                var workinItem = (ReflectionMetadataInfo<TType>)item;

                yield return new MetadataCacheParam
                {
                    Name = workinItem.PropertyName,
                    Value = workinItem.ValueExtractor(instance),
                    Sensitivity = workinItem.Sensitivity
                };
            }
        }

       private  IEnumerable<ReflectionMetadataInfo<TType>> ExtracPropertiesValues<TType>()
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

        private Sensitivity? ExtractSensitivity(PropertyInfo propertyInfo)
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
    }




}
