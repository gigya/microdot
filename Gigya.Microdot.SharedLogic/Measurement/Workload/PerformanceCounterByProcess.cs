using System;
using System.Diagnostics;
using System.Linq;

namespace Gigya.Microdot.SharedLogic.Measurement.Workload
{
    public class PerformanceCounterByProcess : IDisposable
    {
        private readonly string _categoryName;
        private readonly string _counterName;

        private PerformanceCounter _counter;

        public PerformanceCounterByProcess(string categoryName, string counterName)
        {
            _categoryName = categoryName;
            _counterName = counterName;

        }

        public virtual double? GetValue()
        {
            if (_counter == null)
                try
                {
                    _counter = GetCounterByCurrentProcess();
                }
                catch
                {
                    return null;
                }

            try
            {
                return _counter?.NextValue();
            }
            catch
            {
                // maybe process name has changed. Try re-creating the counter
                _counter?.Dispose();
                _counter = GetCounterByCurrentProcess();
                try
                {
                    return _counter?.NextValue();
                }
                catch
                {
                    return null;
                }
            }
        }

        private PerformanceCounter GetCounterByCurrentProcess()
        {
            var instanceName = GetInstanceNameByProcessId(Process.GetCurrentProcess().Id) ?? Process.GetCurrentProcess().ProcessName;
            return new PerformanceCounter(_categoryName, _counterName, instanceName);
        }

        private static string GetInstanceNameByProcessId(int pid)
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            foreach (string instanceName in new PerformanceCounterCategory("Process").GetInstanceNames().Where(i => i.StartsWith(processName)))
            {
                using (var pidCounter = new PerformanceCounter("Process", "ID Process", instanceName, true))
                {
                    if ((int)pidCounter.NextValue() == pid)
                        return instanceName;
                }
            }
            return null;
        }


        public void Dispose()
        {
            _counter?.Dispose();
        }
    }
}
