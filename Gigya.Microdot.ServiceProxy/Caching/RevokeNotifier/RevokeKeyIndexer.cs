using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public class RevokeKeyIndexer : IRevokeKeyIndexer
    {
        private ConcurrentDictionary<string, IRevokeContextConcurrentCollection> _entries;
        private IRevokeContextConcurrentCollectionFactory _contextCollectionFactory;
        private ILog _log;


        public RevokeKeyIndexer(IRevokeContextConcurrentCollectionFactory contextCollectionFactory, ILog log)
        {
            _log = log;
            _entries = new ConcurrentDictionary<string, IRevokeContextConcurrentCollection>();
            _contextCollectionFactory = contextCollectionFactory;
        }

        public bool ContainsKey(string key)
        {
            return _entries.ContainsKey(key);
        }
        public IEnumerable<RevokeContext> GetLiveRevokeesAndSafelyRemoveDeadOnes(string revokeKey)
        {
            if (null == revokeKey)
            {
                throw new NullReferenceException("Key can't be null");
            }

            if (false == _entries.TryGetValue(revokeKey, out var revokees))
            {
                return Enumerable.Empty<RevokeContext>();
            }

            //Cleanup empty entries
            if (revokees.IsEmpty)
            {
                //Entry was populated after we removed it
                if (false == SafelyRemoveEntry(revokeKey, out var revived))
                {
                    return revived;
                }
                return Enumerable.Empty<RevokeContext>();
            }

            return revokees;
        }


        protected bool SafelyRemoveEntry(string revokeKey, out IRevokeContextConcurrentCollection revived)
        {
            revived = null;
            _entries.TryRemove(revokeKey, out var removed);

            //Race condition #1 somebody registered this revoke key after we emptied it. 
            if (removed.IsEmpty == false)
            {
                //Lets try and put it back
                //Race condition #2 we want to add the collection after removing it but somebody created a new one already
                revived = _entries.AddOrUpdate(revokeKey, removed, (_, dictionary) => dictionary.MergeMissingEntriesWith(removed));
                return false;
            }

            _log.Info(logger => logger("Removed entry for revokeKey",
                          unencryptedTags:new
                            {
                                revokeKey
                            }));
            return true;
        }


        public void AddRevokeContext(string key, RevokeContext context)
        {
            if (key == null)
            {
                throw new NullReferenceException("Key can't be null");
            }
            if(context == null)
            {
                throw new NullReferenceException("Context can't be null");
            }
            var collection = _entries.GetOrAdd(key, _ => _contextCollectionFactory.Create());
            collection.Insert(context);
        }


        public bool Remove(object obj, string key)
        {
            if(null == obj)
            {
                throw new NullReferenceException("Object can't be null");
            }

            if(key == null)
            {
                throw new NullReferenceException("Key can't be null");
            }

            if (false == _entries.TryGetValue(key, out var collection))
            {
                return false;
            }

            var res = collection.RemoveEntryMatchingObject(obj);
            //We might have emptied the collection need to cleanup
            if (collection.IsEmpty)
            {
                SafelyRemoveEntry(key,out _);
            }

            return res;
        }


        public void Remove(object obj)
        {
            if (null == obj)
            {
                throw new NullReferenceException("Object can't be null");
            }
            RemoveCore(obj, RemoveLogic);
        }

        protected void RemoveLogic(object obj, KeyValuePair<string, IRevokeContextConcurrentCollection> keyCollection, List<string> toBeRemoved)
        {
            var collection = keyCollection.Value;
            var removed = collection.RemoveEntryMatchingObject(obj);
            if (removed && collection.IsEmpty)
            {
                toBeRemoved.Add(keyCollection.Key);
            }
        }

        //Meant to be use periodically
        public void Cleanup()
        {
            RemoveCore(null, CleanupLogic);
        }

        protected void CleanupLogic(object obj, KeyValuePair<string, IRevokeContextConcurrentCollection> keyCollection, List<string> toBeRemoved)
        {
            var collection = keyCollection.Value;
            var cleanAmount = collection.Cleanup();
            if (cleanAmount > 0)
            {
                _log.Warn(logger => logger("Detected GC collected subscribers",
                              unencryptedTags:
                              new
                              {
                                  revokeKey = keyCollection.Key,
                                  amount = cleanAmount
                              }));

                if (collection.IsEmpty)
                {
                    toBeRemoved.Add(keyCollection.Key);
                }
            }
        }

        protected void RemoveCore(object obj, Action<object, KeyValuePair<string,IRevokeContextConcurrentCollection>, List<string>> actAndMarkForRemoval)
        {
            List<string> toBeRemoved = new List<string>();

            foreach (var keyCollection in _entries)
            {
                actAndMarkForRemoval(obj, keyCollection, toBeRemoved);
            }

            //Clear once empty entries safely
            foreach (var toBeDeletedKey in toBeRemoved)
            {
                SafelyRemoveEntry(toBeDeletedKey, out _);
            }
        }

        public int CountRevokees()
        {
            return _entries.Values.SelectMany(e => e.Select(r => r.Revokee)).Count();
        }
    }



}