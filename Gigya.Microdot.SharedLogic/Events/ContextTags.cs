using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.Events
{
    public struct ContextTag
    {
        public object Value;
        public bool IsEncrypted;
    }

    public class ContextTags
    {
        internal Dictionary<string, ContextTag> Tags;
        public ContextTags() { Tags = new Dictionary<string, ContextTag>(); }
        public ContextTags(Dictionary<string, ContextTag> tags) { Tags = tags; }

        public IEnumerable<KeyValuePair<string, object>> GetUnencryptedTags() => Tags.GetUnencryptedTags();

        public IEnumerable<KeyValuePair<string, object>> GetEncryptedTags() => Tags.GetEncryptedTags();

        public bool TryGet(string key, out object value)
        {
            if (Tags.TryGetValue(key, out var item))
            {
                value = item.Value;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (Tags.TryGetValue(key, out var item))
            {
                try
                {
                    value = (T)Convert.ChangeType(item.Value, typeof(T));
                    return true;
                }
                catch (Exception ex)
                {
                    throw new ProgrammaticException("Cannot obtain tag with desired type", unencrypted: new Tags() {
                        ["error"]       = ex.Message,
                        ["actualType"]  = item.Value?.GetType().ToString(),
                        ["desiredType"] = typeof(T).Name,
                        ["tagName"]     = key,
                    },
                    encrypted: new Tags() { ["tagValue"] = item.Value?.ToString() });
                }
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        public IDisposable SetEncryptedTag<T>(string key, T value)
        {
            return Tag(key, value, IsEncrypted: true);
        }

        public IDisposable SetUnencryptedTag<T>(string key, T value)
        {
            return Tag(key, value, IsEncrypted: false);
        }

        IDisposable Tag<T>(string key, T value, bool IsEncrypted)
        {
            if (!typeof(T).IsValueType && !(value is string))
                throw new ProgrammaticException("Tags can only be pritmive values or strings");

            if (Tags.TryGetValue(key, out var currentTag))
            {
                Tags[key] = new ContextTag { Value = value, IsEncrypted = IsEncrypted };

                // On dispose, we restore the tag to its previous value
                return new DisposableAction<bool>(
                    state: true,
                    dispose: s => Tags[key] = new ContextTag { Value = currentTag.Value, IsEncrypted = currentTag.IsEncrypted });
            }
            else
            {
                Tags[key] = new ContextTag { Value = value, IsEncrypted = IsEncrypted };

                // On dispose, we remove the value
                return new DisposableAction<bool>(
                    state: true,
                    dispose: s => Tags.Remove(key));
            }

        }

    }
}