using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.LanguageExtensions;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gigya.Microdot.Configuration
{

    public sealed class EnvironmentVarialbesConfigurationSource : IHostConfigurationSource
    {
        public string Zone { get; }

        public string Region { get; }

        public string DeploymentEnvironment { get; }

        public string ConsulAddress { get; }

        public CurrentApplicationInfo ApplicationInfo { get; }

        public DirectoryInfo ConfigRoot { get; }

        public FileInfo LoadPathsFile { get; }

        public IDictionary<string, string> CustomKeys => new Dictionary<string, string>();

        public EnvironmentVarialbesConfigurationSource()
        {
            this.ApplicationInfo = null;
            
            this.Zone                  = Environment.GetEnvironmentVariable("ZONE") ?? Environment.GetEnvironmentVariable("DC");
            this.Region                = Environment.GetEnvironmentVariable("REGION");
            this.DeploymentEnvironment = Environment.GetEnvironmentVariable("ENV");
            this.ConsulAddress         = Environment.GetEnvironmentVariable("CONSUL");
            this.ConfigRoot            = Environment.GetEnvironmentVariable("GIGYA_CONFIG_ROOT")      ?.To(x => new DirectoryInfo(x));
            this.LoadPathsFile         = Environment.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE")?.To(x => new FileInfo(x));
        }
    }
}
