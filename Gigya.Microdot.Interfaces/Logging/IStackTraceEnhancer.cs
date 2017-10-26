using System;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Interfaces.Logging
{
    public interface IStackTraceEnhancer
    {
        string Clean(string stackTrace);
        JObject ToJObjectWithBreadcrumb(Exception exception);
    }
}