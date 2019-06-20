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
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.SharedLogic.SystemWrappers
{
    [ConfigurationRoot("dataCenters", RootStrategy.ReplaceClassNameWithPath)]
    public class DataCentersConfig : IConfigObject
    {
        public string Current { get; set; }
    }

    public class EnvironmentInstance : IEnvironment
    {
        private readonly IEnvironmentVariableProvider _environmentVariableProvider;
        private readonly string _region;
        private const string DEFAULT_INSTANCE_NAME = "DefaultInstance";

        private Func<DataCentersConfig> GetDataCentersConfig { get; }

        public EnvironmentInstance(IEnvironmentVariableProvider environmentVariableProvider, Func<DataCentersConfig> getDataCentersConfig, CurrentApplicationInfo applicationInfo)
        {
            _environmentVariableProvider = environmentVariableProvider;
            GetDataCentersConfig = getDataCentersConfig;
            Zone = environmentVariableProvider.GetEnvironmentVariable("ZONE") ?? environmentVariableProvider.GetEnvironmentVariable("DC");
            _region = environmentVariableProvider.GetEnvironmentVariable("REGION");
            DeploymentEnvironment = environmentVariableProvider.GetEnvironmentVariable("ENV");
            ConsulAddress = environmentVariableProvider.GetEnvironmentVariable("CONSUL");
            InstanceName = applicationInfo.InstanceName ?? environmentVariableProvider.GetEnvironmentVariable("GIGYA_SERVICE_INSTANCE_NAME") ?? DEFAULT_INSTANCE_NAME;

            if (string.IsNullOrEmpty(Zone) || string.IsNullOrEmpty(DeploymentEnvironment))
                throw new EnvironmentException("One or more of the following environment variables, which are required, have not been set: %ZONE%, %ENV%");
        }
       
        public string InstanceName { get; }
        public string Zone { get; }
        public string Region => _region ?? GetDataCentersConfig().Current; // if environmentVariable %REGION% does not exist, take the region from DataCenters configuration (the region was previously called "DataCenter")
        public string DeploymentEnvironment { get; }        
        public string ConsulAddress { get; }

        [Obsolete("To be deleted on version 2.0")]
        public void SetEnvironmentVariableForProcess(string name, string value)
        {
            _environmentVariableProvider.SetEnvironmentVariableForProcess(name, value);
        }

        [Obsolete("To be deleted on version 2.0")]
        public string GetEnvironmentVariable(string name) => _environmentVariableProvider.GetEnvironmentVariable(name); 
    }
}