using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.Interfaces.Logging
{
    public interface IStackTraceEnhancer
    {
        string Clean(string stackTrace);
        string AddBreadcrumb(SerializableException exception);
    }
}