#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Linq;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Exceptions;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Configuration
{
    public class EnvironmentVariableProvider : IEnvironmentVariableProvider
    {
        private const string GIGYA_ENV_VARS_FILE = "GIGYA_ENVVARS_FILE";
        private const string ENV_FILEPATH = "{0}/gigya/environmentVariables.json";

        private IFileSystem FileSystem { get; }

        public EnvironmentVariableProvider(IFileSystem fileSystem, CurrentApplicationInfo applicationInfo)
        {

            FileSystem = fileSystem;
            PlatformSpecificPathPrefix = Environment.OSVersion.Platform == PlatformID.Unix ? "/etc" : "D:";

            var locEnvFilePath = GetEnvironmentVariable(GIGYA_ENV_VARS_FILE);

            if (string.IsNullOrEmpty(locEnvFilePath))
            {
                locEnvFilePath = string.Format(ENV_FILEPATH, PlatformSpecificPathPrefix);
            }
            ReadFromFile(locEnvFilePath);

            DataCenter = GetEnvironmentVariable("ZONE") ?? GetEnvironmentVariable("DC");
            DeploymentEnvironment = GetEnvironmentVariable("ENV");
            
            //Should not be allowed override by file
            SetEnvironmentVariableForProcess("AppName", applicationInfo.Name);
        }

        public void SetEnvironmentVariableForProcess(string name, string value)
        {
            Environment.SetEnvironmentVariable(name, value.ToLower(), EnvironmentVariableTarget.Process);
        }

        public string GetEnvironmentVariable(string name) { return Environment.GetEnvironmentVariable(name)?.ToLower(); }

        public string PlatformSpecificPathPrefix { get; }

        [Obsolete("To be deleted on version 2.0")]
        public string DataCenter { get; }

        [Obsolete("To be deleted on version 2.0")]
        public string DeploymentEnvironment { get; }

        /// <summary>
        /// Reads each property in file and sets its environment variable.
        /// </summary>
        /// <returns>Names of environment variables read from file</returns>
        public void ReadFromFile(string locEnvFilePath)
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
                SetEnvironmentVariableForProcess(property.Name, property.Value.Value<string>());
            }
        }

    }
}