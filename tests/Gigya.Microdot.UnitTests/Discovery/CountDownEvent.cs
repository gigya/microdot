using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Gigya.Microdot.UnitTests.Discovery
{
    public class CountDownEvent
    {
        private int _counter;
        private readonly List<KeyValuePair<int, TaskCompletionSource<int>>> _waiting = new List<KeyValuePair<int, TaskCompletionSource<int>>>();

        readonly object _locker = new object();
        public Task SetExpected(int expected, TimeSpan timeOut)
        {
            lock (_locker)
            {

                if (_counter >= expected)
                {
                    Console.WriteLine($"already received {_counter} events");
                    return TaskDone.Done;
                }

                var cancel = new CancellationTokenSource(timeOut);
                var wait = new TaskCompletionSource<int>();
                cancel.Token.Register(
                    () => wait.TrySetException(
                        new Exception(
                            $"Expected events: {expected}. Received events: {_counter}. Timeout after {timeOut.TotalMilliseconds} ms")));
                wait.Task.ContinueWith(x => cancel.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);

                _waiting.Add(new KeyValuePair<int, TaskCompletionSource<int>>(expected, wait));

                return wait.Task;
            }
        }



        public void ReceivedEvent(string eventDescription = null)
        {
            try
            {
                lock (_locker)
                {
                    _counter++;
                    Console.WriteLine($"Received new event, total events {_counter}. EventDescription: {eventDescription}");

                    for (int i = 0; i < _waiting.Count; i++)
                    {
                        var wait = _waiting[i];
                        if (_counter >= wait.Key) wait.Value.TrySetResult(1);
                    }


                    _waiting.RemoveAll(x => x.Value.Task.IsCompleted);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        public int TotalEvents()
        {
            lock (_locker)
            {
                return _counter;
            }
        }

    }
}