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
using System.IO;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.SharedLogic.SystemWrappers
{
    [ConfigurationRoot("dataCenters", RootStrategy.ReplaceClassNameWithPath)]
    public class DataCentersConfig : IConfigObject
    {
        public string Current { get; set; }
    }

    public class EnvironmentInstance : IEnvironment
    {
        private readonly string _region;
        private const string DEFAULT_INSTANCE_NAME = "DefaultInstance";
        
        private const string GIGYA_CONFIG_ROOT_DEFAULT = "config";
        private const string LOADPATHS_JSON = "loadPaths.json";

        private const string GIGYA_CONFIG_ROOT_KEY = "GIGYA_CONFIG_ROOT";
        private const string GIGYA_CONFIG_PATHS_FILE_KEY = "GIGYA_CONFIG_PATHS_FILE";

        private Func<DataCentersConfig> GetDataCentersConfig { get; }

        public EnvironmentInstance(Func<DataCentersConfig> getDataCentersConfig, CurrentApplicationInfo applicationInfo)
        {
            GetDataCentersConfig = getDataCentersConfig;
            Zone = Environment.GetEnvironmentVariable("ZONE") ?? Environment.GetEnvironmentVariable("DC");
            _region = Environment.GetEnvironmentVariable("REGION");
            DeploymentEnvironment = Environment.GetEnvironmentVariable("ENV");
            ConsulAddress = Environment.GetEnvironmentVariable("CONSUL");
            InstanceName = applicationInfo.InstanceName ?? Environment.GetEnvironmentVariable("GIGYA_SERVICE_INSTANCE_NAME") ?? DEFAULT_INSTANCE_NAME;

            if (string.IsNullOrEmpty(Zone) || string.IsNullOrEmpty(DeploymentEnvironment))
                throw new EnvironmentException("One or more of the following environment variables, which are required, have not been set: %ZONE%, %ENV%");

            ConfigRoot = GetConfigRoot();
            LoadPathsFile = GetLoadPathsFile();
        }

        private DirectoryInfo GetConfigRoot()
        {
            var configRootPath =
                            Environment.GetEnvironmentVariable(GIGYA_CONFIG_ROOT_KEY).NullWhenEmpty()
                            ?? Path.Combine(Environment.CurrentDirectory, GIGYA_CONFIG_ROOT_DEFAULT);

            var dirInfo = new DirectoryInfo(configRootPath);

            if (dirInfo.Exists == false)
                throw new EnvironmentException(
                    $"ConfigRoot path doesn't exist '{ dirInfo.FullName }'. " +
                    $"Use '{GIGYA_CONFIG_ROOT_KEY}' environment variable to override default path.");

            return dirInfo;
        }

        private FileInfo GetLoadPathsFile()
        {
            var loadPathsFilePath =
                            Environment.GetEnvironmentVariable(GIGYA_CONFIG_PATHS_FILE_KEY).NullWhenEmpty()
                            ?? Path.Combine(ConfigRoot.FullName, LOADPATHS_JSON);

            var fileInfo = new FileInfo(loadPathsFilePath);

            if (fileInfo.Exists == false)
                throw new EnvironmentException(
                    $"LoadPaths file isn't found at '{ fileInfo.FullName }'. " +
                    $"Use '{GIGYA_CONFIG_PATHS_FILE_KEY}' environment variable to define absolute path" +
                    $"to the file or place a 'loadPaths.json' at your config root.");

            return fileInfo;
        }


        public string InstanceName { get; }
        public string Zone { get; }
        public string Region => _region ?? GetDataCentersConfig().Current; // if environmentVariable %REGION% does not exist, take the region from DataCenters configuration (the region was previously called "DataCenter")
        public string DeploymentEnvironment { get; }        
        public string ConsulAddress { get; }
        public DirectoryInfo ConfigRoot { get; }
        public FileInfo LoadPathsFile { get; }

        [Obsolete("To be deleted on version 2.0")]
        public void SetEnvironmentVariableForProcess(string name, string value)
            => Environment.SetEnvironmentVariable(name, value);

        [Obsolete("To be deleted on version 2.0")]
        public string GetEnvironmentVariable(string name)
            => Environment.GetEnvironmentVariable(name);
    }
}