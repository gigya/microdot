using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService;

namespace Gigya.Microdot.Hosting.Service
{
    public class ServiceWarmup : IWarmup
    {
        public bool IsServiceWarmed { get; } = true;
        public Task WaitForWarmup()
        {
            return Task.FromResult(true);
        }

        public void Warmup()
        {
        }
    }
}
