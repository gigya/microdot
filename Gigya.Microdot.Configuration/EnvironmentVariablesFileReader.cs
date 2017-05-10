using System;
using System.Linq;

using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Exceptions;

using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Configuration
{
    /// <summary>
    /// Will try to check if file C:\gigya\environmentVariables.json exists 
    /// and will try to read content it expects a single object where each property is a name of environment variable
    /// {
    ///     "EnvVar1":"Value",
    ///     "EnvVar1":"Value"
    /// }
    /// </summary>
    public class EnvironmentVariablesFileReader
    {
        private const string GIGYA_ENV_VARS_FILE = "GIGYA_ENVVARS_FILE";
        private const string ENV_FILEPATH = @"{0}/gigya/environmentVariables.json";
        private readonly string locEnvFilePath;

        private IEnvironment Environment { get; }
        private IFileSystem FileSystem { get; }


        /// <summary>
        /// Parses the content of environment variables file content.
        /// </summary>        
        public EnvironmentVariablesFileReader(IFileSystem fileSystem, IEnvironment environment)
        {
            locEnvFilePath = environment.GetEnvironmentVariable(GIGYA_ENV_VARS_FILE);

            if (string.IsNullOrEmpty(locEnvFilePath))
            {
                locEnvFilePath = string.Format(ENV_FILEPATH, environment.PlatformSpecificPathPrefix);
            }
           
            Environment = environment;
            FileSystem = fileSystem;
        }


        /// <summary>
        /// Reads each property in file and sets its environment variable.
        /// </summary>
        /// <returns>Names of environment variables read from file</returns>
        public void ReadFromFile()
        {
            JObject envVarsObject;

            try
            {
                var text = FileSystem.TryReadAllTextFromFile(locEnvFilePath);

                if (string.IsNullOrEmpty(text))
                    return;

                envVarsObject = JObject.Parse(text);
            }
            catch (Exception ex)
            {
                throw new ConfigurationException($"Missing or invalid configuration file: {locEnvFilePath}", ex);
            }

            if (envVarsObject == null)
                return;

            var properties = envVarsObject.Properties().Where(a => a.HasValues).ToArray();

            foreach (var property in properties)
            {
                 Environment.SetEnvironmentVariableForProcess(property.Name, property.Value.Value<string>());
            }
        }
    }
}