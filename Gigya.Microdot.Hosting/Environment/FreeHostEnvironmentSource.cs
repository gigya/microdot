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

using System.Collections.Generic;
using System.IO;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Hosting.Environment
{
    public sealed class FreeHostEnvironmentSource : IHostEnvironmentSource
    {
        public string Zone { get; }

        public string Region { get; }

        public string DeploymentEnvironment { get; }

        public string ConsulAddress { get; }

        public string InstanceName { get; }

        public CurrentApplicationInfo ApplicationInfo { get; }

        public DirectoryInfo ConfigRoot { get; }

        public FileInfo LoadPathsFile { get; }

        public IDictionary<string, string> EnvironmentVariables { get; }

        public FreeHostEnvironmentSource(
            string zone = null,
            string region = null,
            string deploymentEnvironment = null,
            string consulAddress = null,
            string instanceName = null,
            CurrentApplicationInfo applicationInfo = null,
            DirectoryInfo configRoot = null,
            FileInfo loadPathsFile = null,
            Dictionary<string, string> customKeys = null,
            string appName = null)
        {
            this.Zone = zone;
            this.Region = region;
            this.DeploymentEnvironment = deploymentEnvironment;
            this.ConsulAddress = consulAddress;
            this.InstanceName = instanceName;
            this.ApplicationInfo = applicationInfo;
            this.ConfigRoot = configRoot;
            this.LoadPathsFile = loadPathsFile;
            this.EnvironmentVariables = customKeys ?? new Dictionary<string, string>();
        }
    }
}
