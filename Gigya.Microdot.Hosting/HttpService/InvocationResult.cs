using System;

namespace Gigya.Microdot.Hosting.HttpService
{
    public class InvocationResult
    {
        public object Result { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}