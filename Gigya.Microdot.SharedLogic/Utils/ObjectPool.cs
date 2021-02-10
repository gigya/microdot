using System;
using System.Collections.Concurrent;

namespace Gigya.Microdot.SharedLogic.Utils
{
    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectGenerator;
        private readonly int _poolSize;

        public ObjectPool(Func<T> objectGenerator, int poolSize)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
            _poolSize = poolSize;
        }

        public T Get() => _objects.TryTake(out T item) ? item : _objectGenerator();

        public void Return(T item)
        {
            if (_objects.Count >= _poolSize)
                return;

            _objects.Add(item);
        }
    }
}
