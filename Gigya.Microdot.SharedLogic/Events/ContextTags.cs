using System;
using System.Collections.Generic;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class ContextTags : Dictionary<string, (object value, bool unencryptedLog, bool encryptedLog)>
    {
        public IEnumerable<KeyValuePair<string, object>> GetUnencryptedLog()
        {
            return this
                .Where(e => e.Value.unencryptedLog)
                .Select(e => new KeyValuePair<string, object>(e.Key, e.Value.value));
        }

        public IEnumerable<KeyValuePair<string, object>> GetEncryptedLog()
        {
            return this
                .Where(e => e.Value.encryptedLog)
                .Select(e => new KeyValuePair<string, object>(e.Key, e.Value.value));
        }

        public T Get<T>(string key)
        {
            TryGetValue(key, out var tag);
            return tag.value is T tagValue ? tagValue : default(T);

        }

        public IDisposable Tag<T>(string key, T value, bool unencryptedLog = false, bool encryptedLog = false)
        {
            TryGetValue(key, out var currentTag);
            this[key] = (value, unencryptedLog, encryptedLog);

            return new DisposableAction<(string key, object value, bool unencryptedLog, bool encryptedLog)>(
                state: (key, currentTag.value, currentTag.unencryptedLog, currentTag.encryptedLog),
                dispose: s => TracingContext.Tags()[s.key] = (s.value, s.unencryptedLog, s.encryptedLog));
        } 
 
    }
}