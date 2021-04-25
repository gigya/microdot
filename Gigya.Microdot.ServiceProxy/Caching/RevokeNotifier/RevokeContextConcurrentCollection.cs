using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public class RevokeContextConcurrentCollection : IRevokeContextConcurrentCollection
    {
        private readonly ConcurrentDictionary<EquatableWeakReference<object>,RevokeContext> _collection;

        public RevokeContextConcurrentCollection()
        {
            //I don't expose the state of the object as a contractor parameter as this will require yet another class and you got to 
            //put a stop to abstraction at some point.
            _collection = new ConcurrentDictionary<EquatableWeakReference<object>, RevokeContext>();
        }
        public IEnumerator<RevokeContext> GetEnumerator()
        {
            return new RevokeContextConcurrentCollectionEnumerator(_collection);
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        //This is called frequently, so no cleanup is performed here. 
        public bool IsEmpty => _collection.IsEmpty;

        public IRevokeContextConcurrentCollection MergeMissingEntriesWith(IRevokeContextConcurrentCollection other)
        {
            if (other == null)
            {
                return this;
            }

            foreach (var entry in other)
            {
                var otherObj = entry.Revokee;
                //Doing best effort not to insert nulls
                if (otherObj == null)
                {
                    continue;
                }
                
                _collection.TryAdd(new EquatableWeakReference<object>(otherObj), entry);
            }

            return this;
        }

        public void Insert(RevokeContext context)
        {
            if (context == null)
            {
                throw new NullReferenceException("RevokeContext can not be null");
            }
            var revokee = context.Revokee;
            if (revokee != null)
            {
                var key = new EquatableWeakReference<object>(revokee);
                var value = context;

                //Override
                _collection.AddOrUpdate(key, value,(_,__) => value);
            }
        }

        public bool RemoveEntryMatchingObject(object obj)
        {
            if (null == obj)
            {
                return false;
            }
            var keyToRemove = new EquatableWeakReference<object>(obj);
            return _collection.TryRemove(keyToRemove, out _);
        }


        public int Cleanup()
        {
            var toBeDeletedEntries = new List<EquatableWeakReference<object>>();
            foreach (var key in _collection.Keys)
            {
                var target = key.Target;
                if (target == null)
                {
                    toBeDeletedEntries.Add(key);
                }
            }

            foreach (var key in toBeDeletedEntries)
            {
                _collection.TryRemove(key, out _);
            }

            return toBeDeletedEntries.Count;
        }



        private class RevokeContextConcurrentCollectionEnumerator : IEnumerator<RevokeContext>
        {
            private IEnumerator<KeyValuePair<EquatableWeakReference<object>, RevokeContext>> _enumerator;

            private static readonly KeyValuePair<EquatableWeakReference<object>, RevokeContext> _defaultKeyValuePair = default;

            public RevokeContextConcurrentCollectionEnumerator(ConcurrentDictionary<EquatableWeakReference<object>, RevokeContext> collection)
            {
                _enumerator = collection.GetEnumerator();
            }

            public bool MoveNext()
            {
                //Advance to the first item that contains an object or the end of the collection
                while (_enumerator.MoveNext())
                {
                    if (TryGetCurrent(out _))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool TryGetCurrent(out RevokeContext context)
            {
                context = null;
                var current = _enumerator.Current;
                //Got to the end of the collection
                if (current.Equals(_defaultKeyValuePair))
                {
                    return false;
                }

                var currentKey = current.Key;
                //This should not happen as the key is strongly referenced but better safe than sorry
                if (currentKey == null)
                {
                    return false;
                }
                //Our target got collected
                if (currentKey.Target == null)
                {
                    return false;
                }

                var currentValue = current.Value;
                if (currentValue == null)
                {
                    return false;
                }

                context = currentValue;
                return true;
            }


            public void Reset()
            {
                _enumerator.Reset();
            }


            public RevokeContext Current => TryGetCurrent(out var context) ? context : null;

            object IEnumerator.Current => Current;


            public void Dispose()
            {
            }
        }
    }
}