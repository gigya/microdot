using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gigya.Microdot.Common.Tests
{
    public sealed class TestHostConfigurationSource : IHostConfigurationSource
    {
        public string Zone { get; }

        public string Region { get; }

        public string DeploymentEnvironment { get; }

        public string ConsulAddress { get; }

        public CurrentApplicationInfo ApplicationInfo { get; }

        public DirectoryInfo ConfigRoot { get; }

        public FileInfo LoadPathsFile { get; }

        public TestHostConfigurationSource(
            string zone = null,
            string region = null,
            string deploymentEnvironment = null,
            string consulAddress = null,
            CurrentApplicationInfo applicationInfo = null,
            DirectoryInfo configRoot = null,
            FileInfo loadPathsFile = null)
        {
            this.Zone = zone ?? "zone";
            this.Region = region ?? "region";
            this.DeploymentEnvironment = deploymentEnvironment ?? "env";
            this.ConsulAddress = consulAddress ?? "addr";
            this.ApplicationInfo = applicationInfo ?? new CurrentApplicationInfo("", Environment.UserName, "");
            this.ConfigRoot = configRoot ?? new DirectoryInfo(Directory.GetCurrentDirectory());
            this.LoadPathsFile = loadPathsFile ?? new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "loadPaths.json"));
        }
    }
}
