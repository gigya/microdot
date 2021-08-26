using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

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
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?_counter?.NextValue() : null;
            }
            catch
            {
                // maybe process name has changed. Try re-creating the counter
                _counter?.Dispose();
                _counter = GetCounterByCurrentProcess();
                try
                {
                    return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _counter?.NextValue() : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        private PerformanceCounter GetCounterByCurrentProcess()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var instanceName = GetInstanceNameByProcessId(Process.GetCurrentProcess().Id) ??
                                   Process.GetCurrentProcess().ProcessName;
                return new PerformanceCounter(_categoryName, _counterName, instanceName);
            }

            return null;
        }

        private static string GetInstanceNameByProcessId(int pid)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var processName = Process.GetCurrentProcess().ProcessName;
                foreach (string instanceName in new PerformanceCounterCategory("Process").GetInstanceNames()
                    .Where(i => i.StartsWith(processName)))
                {
                    using (var pidCounter = new PerformanceCounter("Process", "ID Process", instanceName, true))
                    {
                        if ((int)pidCounter.NextValue() == pid)
                            return instanceName;
                    }
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
