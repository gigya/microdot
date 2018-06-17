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
using Gigya.Microdot.Interfaces.Logging;
using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.SharedLogic.Events
{

    public class ReflectionMetadataInfo
    {
        public string Name { get; set; }
        public Func<object, object> ValueExtractor { get; set; }
        public Sensitivity? Sensitivity { get; set; }
    }

    public interface IPropertiesMetadataPropertiesCache
    {
        IEnumerable<MetadataCacheParam> ParseIntoParams(object instance);
    }

    public class MetadataCacheParam
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public Sensitivity? Sensitivity { get; set; }

    }


    public class PropertiesMetadataPropertiesCache : IPropertiesMetadataPropertiesCache
    {
        private readonly ILog _log;
        private readonly ConcurrentDictionary<Type, ReflectionMetadataInfo[]> _propertyMetadataCache;

        public PropertiesMetadataPropertiesCache(ILog log)
        {
            _log = log;
            _propertyMetadataCache = new ConcurrentDictionary<Type, ReflectionMetadataInfo[]>();

        }

        public IEnumerable<MetadataCacheParam> ParseIntoParams(object instance)
        {
            var type = instance.GetType();
            var result = ExtracParams(instance, type);

            return result;
        }

        private IEnumerable<MetadataCacheParam> ExtracParams(object instance, Type type)
        {
            var propertyMetadata = _propertyMetadataCache.GetOrAdd(type, x => ExtracPropertiesMetadata(instance, type).ToArray());

            foreach (var item in propertyMetadata)
            {
                object value;

                try
                {
                    value = item.ValueExtractor(instance);
                }
                catch (Exception ex)
                {
                    _log.Warn("This property is invalid", unencryptedTags: new { propertyName = item.Name }, exception: ex);
                    continue;
                }

                yield return new MetadataCacheParam
                {
                    Name = item.Name,
                    Value = value,
                    Sensitivity = item.Sensitivity
                };
            }
        }

        internal static IEnumerable<ReflectionMetadataInfo> ExtracPropertiesMetadata(object instance, Type type)
        {
            foreach (var member in type.FindMembers(MemberTypes.Property | MemberTypes.Field, BindingFlags.Public | BindingFlags.Instance, null, null))
            {
                var instanceParameter = Expression.Parameter(typeof(object), "target");
                MemberExpression memberExpression = null;
                if (member.MemberType == MemberTypes.Property)
                {
                    memberExpression = Expression.Property(Expression.Convert(instanceParameter, member.DeclaringType), (PropertyInfo)member);
                }
                else
                {
                    if (member.MemberType == MemberTypes.Field)
                    {
                        memberExpression = Expression.Field(Expression.Convert(instanceParameter, member.DeclaringType), (FieldInfo)member);
                    }
                }

                var lambda = Expression.Lambda<Func<object, object>>(Expression.Convert(memberExpression, typeof(object)), instanceParameter);
                yield return new ReflectionMetadataInfo
                {
                    Name = member.Name,
                    ValueExtractor = lambda.Compile(),
                    Sensitivity = ExtractSensitivity(member)
                };
            }
        }

        internal static Sensitivity? ExtractSensitivity(MemberInfo memberInfo)
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
                return Sensitivity.NonSensitive;
            }

            return null;
        }
    }
}
