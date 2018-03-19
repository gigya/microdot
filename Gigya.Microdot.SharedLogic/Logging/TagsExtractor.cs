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
using System.Reflection;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.SharedLogic.Logging
{


	public static class TagsExtractor
	{
        private const string prefix = "tags.";

        public static IEnumerable<KeyValuePair<string, object>> GetTagsFromObject(object tagsObject)
        {
            if (tagsObject == null)
                return Enumerable.Empty<KeyValuePair<string, object>>();
            else if (tagsObject is IEnumerable<KeyValuePair<string, object>> objectsList)
                return objectsList;
            else if (tagsObject is IEnumerable<KeyValuePair<string, string>> stringsList)
                return stringsList.Select(_ => new KeyValuePair<string, object>(_.Key, _.Value));
            else return TagsObjectCache.GetOrAdd(tagsObject.GetType(), t => CreateObjectCachedReflectionFuncs(t).ToList())
                   .Select(_ => new KeyValuePair<string, object>(_.Name, _.GetValueFunc(tagsObject)));
        }

        private static readonly ConcurrentDictionary<Type, IList<ReflectionResult>> TagsObjectCache = new ConcurrentDictionary<Type, IList<ReflectionResult>>();

        private static IEnumerable<ReflectionResult> CreateObjectCachedReflectionFuncs(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => new ReflectionResult(p.Name, p.GetValue))
                   .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => new ReflectionResult(f.Name, f.GetValue)));
        }

        private class ReflectionResult
        {
            internal readonly string Name;
            internal readonly Func<object, object> GetValueFunc;

            internal ReflectionResult(string name, Func<object, object> getValueFunc)
            {
                Name = name;
                GetValueFunc = getValueFunc;
            }
        }


        public static IEnumerable<KeyValuePair<string, object>> GetUnencryptedTags(this Exception ex)
        {
            return ex.GetAllExceptions()
                   .OfType<SerializableException>()
                   .Where(_ => _.UnencryptedTags != null)
                   .SelectMany(e => e.UnencryptedTags)
                   .Select(_ => new KeyValuePair<string, object>(_.Key, _.Value)); // Hopefully exceptions will contain object tags in the future and we can remove this
        }


	    public static IEnumerable<KeyValuePair<string, object>> GetEncryptedTagsAndExtendedProperties(this Exception exception)
	    {
	        var toReturn = Enumerable.Empty<KeyValuePair<string, object>>();

            foreach (var ex in exception.GetAllExceptions().OfType<SerializableException>())
	        {
	            if(ex.EncryptedTags != null)
	            {
	                var encTags = ex.EncryptedTags.Select(_ => new KeyValuePair<string, object>(_.Key, _.Value));
	                toReturn = toReturn.Concat(encTags);
	            }

                var extended=ex.GetCustomAndExtendedProperties()
                  .GroupBy(p => p.Key)
                  .Select(g => new KeyValuePair<string, object>(g.Key, string.Join("\n", g.Select(kvp => kvp.Value.ToString()))));

	            toReturn = toReturn.Concat(extended);
	        }

	        return toReturn;
	    }


        static IEnumerable<Exception> GetAllExceptions(this Exception ex)
        {
            while (ex != null)
            {
                yield return ex;
                ex = ex.InnerException;
            }
        }


        public static IEnumerable<KeyValuePair<string, string>> MergeDuplicateTags(this IEnumerable<KeyValuePair<string, string>> tags)
        {
            return tags.GroupBy(_ => _.Key).Select(_ => new KeyValuePair<string, string>(_.Key, string.Join("\n", _.Select(a => a.Value).Distinct())));
        }


        public static IEnumerable<KeyValuePair<string, string>> FormatTagsWithoutTypeSuffix(this IEnumerable<KeyValuePair<string, object>> tags)
        {            
            return tags.Select(tag => new KeyValuePair<string, string>(prefix + tag.Key, EventFieldFormatter.SerializeFieldValue(tag.Value)));
        }


	    public static IEnumerable<KeyValuePair<string, string>> FormatTagsWithTypeSuffix(this IEnumerable<KeyValuePair<string, object>> tags)
        {
            foreach (var tag in tags)
            {
                EventFieldFormatter.SerializeFieldValueAndTypeSuffix(tag.Value, out string value, out string suffix);
                yield return new KeyValuePair<string, string>(prefix + tag.Key + suffix, value);
            }
        }


    }
}
