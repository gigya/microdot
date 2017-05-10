namespace Gigya.Microdot.Interfaces.SystemWrappers
{
    public interface IEnvironment
    {
        void SetEnvironmentVariableForProcess(string name, string value);

        string GetEnvironmentVariable(string name);

        string PlatformSpecificPathPrefix { get; }
    }
}