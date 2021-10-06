using Newtonsoft.Json.Linq;
using System;

namespace Gigya.Microdot.Interfaces.Logging
{
    public interface IStackTraceEnhancer
    {
        string Clean(string stackTrace);
        JObject ToJObjectWithBreadcrumb(Exception exception);
    }
}