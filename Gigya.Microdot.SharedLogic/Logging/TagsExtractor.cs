using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Gigya.Microdot.ServiceContract.Exceptions;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.SharedLogic.Logging
{


	public static class TagsExtractor
	{
        private const string prefix = "tags.";

        public static IEnumerable<KeyValuePair<string, object>> GetTagsFromObject(object tagsObject)
        {
            if(tagsObject==null)
                return Enumerable.Empty<KeyValuePair<string, object>>();
            return TagsObjectCache.GetOrAdd(tagsObject.GetType(), t => CreateObjectCachedReflectionFuncs(t).ToList())
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
                string value, suffix;
                EventFieldFormatter.SerializeFieldValueAndTypeSuffix(tag.Value, out value, out suffix);
                yield return new KeyValuePair<string, string>(prefix + tag.Key + suffix, value);
            }
        }


    }
}
