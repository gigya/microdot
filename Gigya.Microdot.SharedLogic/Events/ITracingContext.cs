using System.Collections.Generic;
using Gigya.Microdot.Interfaces.HttpService;

namespace Gigya.Microdot.SharedLogic.Events
{
    public interface ITracingContext 
    {
        string RequestID { get; set; }
        string SpanID { get; }
        string ParentSpnaID { get; }
        IList<HostOverride> Overrides { get; set; }

        void SetSpan(string spanId, string parentSpanId); 
        void SetHostOverride(string serviceName, string host, int? port = null);
        HostOverride GetHostOverride(string serviceName);

        IDictionary<string, object> Export();
    }
}
