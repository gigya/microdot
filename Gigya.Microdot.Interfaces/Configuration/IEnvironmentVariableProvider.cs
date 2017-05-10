namespace Gigya.Microdot.Interfaces.Configuration
{
    public interface IEnvironmentVariableProvider
    {
        /// <summary>
        /// Initialized with environment variable DC
        /// </summary>
        string DataCenter { get; }

        /// <summary>
        /// Initialized with environment variable ENV
        /// </summary>
        string DeploymentEnvironment { get; }

        string ConsulAddress { get; }

        string GetEnvironmentVariable(string name);
    }
}