using Gigya.Microdot.Hosting.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.LanguageExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
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

        public IDictionary<string, string> CustomKeys { get; }

        public TestHostConfigurationSource(
            string                     zone                  = null,
            string                     region                = null,
            string                     deploymentEnvironment = null,
            string                     consulAddress         = null,
            CurrentApplicationInfo     applicationInfo       = null,
            DirectoryInfo              configRoot            = null,
            FileInfo                   loadPathsFile         = null,
            Dictionary<string, string> customKeys            = null,
            string                     appName               = null)
        {
            this.Zone                  = zone                  ?? "zone";
            this.Region                = region                ?? "region";
            this.DeploymentEnvironment = deploymentEnvironment ?? "env";
            this.ConsulAddress         = consulAddress         ?? "addr";
            this.ApplicationInfo       = applicationInfo       ?? new CurrentApplicationInfo(appName ?? "test", Environment.UserName, Dns.GetHostName());
            this.ConfigRoot            = configRoot            ?? new DirectoryInfo(this.GetType().Assembly.Location.To(Path.GetDirectoryName));
            this.LoadPathsFile         = loadPathsFile;
            this.CustomKeys            = customKeys            ?? new Dictionary<string, string>();
        }
    }
}
