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
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class ReflectionMetadataInfo
    {
        public string Name { get; set; }
        public Func<object, object> ValueExtractor { get; set; }
        public Sensitivity? Sensitivity { get; set; }
        public ReflectionMetadataInfo[] InnerMembers { get; set; }
    }

    public interface IMembersToLogExtractor
    {
        IEnumerable<MetadataCacheParam> ExtractMembersToLog(object instance);
    }

    public class MetadataCacheParam
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public Sensitivity? Sensitivity { get; set; }

    }

    public class MembersToLogExtractor : IMembersToLogExtractor
    {
        private readonly ILog _log;
        private readonly ConcurrentDictionary<Type, ReflectionMetadataInfo[]> _membersMetadataCache;

        public MembersToLogExtractor(ILog log)
        {
            _log = log;
            _membersMetadataCache = new ConcurrentDictionary<Type, ReflectionMetadataInfo[]>();
        }

        public IEnumerable<MetadataCacheParam> ExtractMembersToLog(object instance)
        {
            var type = instance.GetType();
            var propertiesMetadata = _membersMetadataCache.GetOrAdd(type, x => ExtractMemberMetadata(type).ToArray());

            return ExtractMembers(instance, propertiesMetadata);
        }

        private IEnumerable<MetadataCacheParam> ExtractMembers(object instance, IEnumerable<ReflectionMetadataInfo> membersMetadata, string containingName = null)
        {
            foreach (var memberMetadata in membersMetadata)
            {
                object value;

                try
                {
                    value = memberMetadata.ValueExtractor(instance);
                }
                catch (Exception ex)
                {
                    _log.Warn("This member is invalid", unencryptedTags: new { propertyName = memberMetadata.Name }, exception: ex);
                    continue;
                }

                var memberName = containingName == null ? memberMetadata.Name : $"{containingName}_{memberMetadata.Name}";

                if (memberMetadata.InnerMembers != null && memberMetadata.InnerMembers.Length > 0)                
                    foreach (var metadataCacheParam in ExtractMembers(value, memberMetadata.InnerMembers, memberName)) yield return metadataCacheParam;                
                else //In case the if flow was executed, we don't want to return the current item
                     //because it's a generic payload (class) and not a member
                {
                    yield return new MetadataCacheParam
                    {
                        Name = memberName,
                        Value = value,
                        Sensitivity = memberMetadata.Sensitivity
                    };
                }
            }
        }

        private const int MaxRecursionDepth = 1;

        internal IEnumerable<ReflectionMetadataInfo> ExtractMemberMetadata(Type type, int recursionDepth = 0,
            Sensitivity? parentSensitivity = null)
        {
            var members = type.FindMembers(MemberTypes.Property | MemberTypes.Field,
                    BindingFlags.Public | BindingFlags.Instance, null, null)
                .Where(x => x is FieldInfo || ((x is PropertyInfo propertyInfo) && propertyInfo.CanRead));

            var typeArguments = recursionDepth < MaxRecursionDepth && type.IsGenericType
                ? type.GetGenericArguments()
                : null;                     

            foreach (var member in members)
            {
                var instanceParameter = Expression.Parameter(typeof(object), "target");
                MemberExpression memberExpression = null;

                if (member.MemberType == MemberTypes.Property)
                    memberExpression = Expression.Property(Expression.Convert(instanceParameter, member.DeclaringType),
                        (PropertyInfo) member);
                else if (member.MemberType == MemberTypes.Field)
                    memberExpression = Expression.Field(Expression.Convert(instanceParameter, member.DeclaringType),
                        (FieldInfo) member);

                var converter = Expression.Convert(memberExpression, typeof(object));
                var lambda = Expression.Lambda<Func<object, object>>(converter, instanceParameter);

                var memberSensitivity = ExtractSensitivity(member) ?? parentSensitivity;

                var memberType = GetMemberUnderlyingType(member);

              
             

                ReflectionMetadataInfo[] innerMembers = null;
                bool hasInnerMembers = memberType != typeof(JObject) && memberType != typeof(JArray) &&
                                       memberType != typeof(JToken) &&
                                       (typeArguments != null && typeArguments.Contains(memberType));
                try
                {
                  if (hasInnerMembers)
                  {
                      innerMembers = ExtractMemberMetadata(memberType, recursionDepth + 1, memberSensitivity)
                          .ToArray();
                  }
                }
                catch (Exception e)
                {
                   _log.Error("can not extractMemberMetadata ",unencryptedTags:new { memberType, memberName = member.Name },exception:e);
                }

                yield return new ReflectionMetadataInfo
                {
                    Name = member.Name,
                    ValueExtractor = lambda.Compile(),
                    Sensitivity = memberSensitivity,
                    InnerMembers = innerMembers
                };
            }
        }

        private Type GetMemberUnderlyingType(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;                
                default:
                    throw new ArgumentException("MemberInfo must be of type FieldInfo or PropertyInfo", "member");
            }
        }


        internal Sensitivity? ExtractSensitivity(MemberInfo memberInfo)
        {
            if (memberInfo.GetCustomAttribute<SensitiveAttribute>()?.Secretive == true)
                return Sensitivity.Secretive;

            else if (memberInfo.GetCustomAttribute<SensitiveAttribute>()?.Secretive == false)
                return Sensitivity.Sensitive;

            else if (memberInfo.GetCustomAttribute<NonSensitiveAttribute>() != null)
                return Sensitivity.NonSensitive;

            else return null;
        }
    }
}
