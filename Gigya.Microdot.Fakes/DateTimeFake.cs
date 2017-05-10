using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Fakes
{
    public class DateTimeFake: IDateTime
    {
        public DateTime UtcNow { get; set; }

        private TaskCompletionSource<bool> _delayTask = new TaskCompletionSource<bool>();

        public List<TimeSpan> DelaysRequested { get; } = new List<TimeSpan>();

        public Task Delay(TimeSpan delay)
        {
            DelaysRequested.Add(delay);
            return _delayTask.Task;
        }

        /// <summary>
        /// Stop current delay
        /// </summary>
        public void StopDelay()
        {
            var previousDelayTask = _delayTask;
            _delayTask = new TaskCompletionSource<bool>();
            previousDelayTask.SetResult(true);
        }
    }
}
