using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.SharedLogic.Collections
{
    /// <summary>
    /// A general purpose queue (FIFO) to keep items queued after a cut off time.
    /// </summary>
    /// <remarks>
    /// Items expected to be queued sequentially in time while the next queued greater or equal to previous 'now'.
    /// If condition violated, the dequeue will keep items out of expected order.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class TimeBoundConcurrentQueue<T>
    {
        public struct Item
        {
            public DateTimeOffset Time;
            public T Data;
        }
        private readonly ConcurrentQueue<Item> _queue = new ConcurrentQueue<Item>();

        public int Count => _queue.Count;

        public void Enqueue(DateTimeOffset now, T data)
        {
            _queue.Enqueue(new Item { Time = now, Data = data });
        }

        /// <summary>
        /// Dequeues and returns items from the queue as long as their <see cref="Item.Time"/> is older or equal to the provided time.
        /// </summary>
        /// <param name="olderThanOrEqual">The cut off time to dequeue items older or equal than.</param>
        public ICollection<Item> Dequeue(DateTimeOffset olderThanOrEqual) 
        {
            var oldItems = new List<Item>();
            lock (_queue)
                // Break, if an empty queue or an item is younger
                while (_queue.TryPeek(out var item) && item.Time <= olderThanOrEqual)
                    if (_queue.TryDequeue(out item))
                            oldItems.Add(item);
                        else
                            GAssert.Fail("Failed to dequeue the item.");
            return oldItems;
        }
    }
}