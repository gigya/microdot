using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Configuration
{
    public class EnvironmentVariableProvider : IEnvironmentVariableProvider
    {        
        private readonly IEnvironment _environment;
        
        public EnvironmentVariableProvider(IEnvironment environment, EnvironmentVariablesFileReader fileReader)
        {
            _environment = environment;

            fileReader.ReadFromFile();
            DataCenter = environment.GetEnvironmentVariable("DC");
            DeploymentEnvironment = environment.GetEnvironmentVariable("ENV");
            ConsulAddress = environment.GetEnvironmentVariable("CONSUL");
        }

        /// Initialized with environment variable CONSUL
        public string ConsulAddress { get; }

        /// <summary>
        /// Initialized with environment variable DC
        /// </summary>
        public string DataCenter { get; }

        /// <summary>
        /// Initialized with environment variable ENV
        /// </summary>
        public string DeploymentEnvironment { get; }

        public string GetEnvironmentVariable(string name) { return _environment.GetEnvironmentVariable(name); }

    }
}