using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.LanguageExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gigya.Microdot.Hosting.Configuration
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

        public IDictionary<string, string> CustomKeys { get; }

        public EnvironmentVarialbesConfigurationSource()
        {
            this.ApplicationInfo = null;

            this.Zone = Environment.GetEnvironmentVariable("ZONE") ?? Environment.GetEnvironmentVariable("DC");
            this.Region = Environment.GetEnvironmentVariable("REGION");
            this.DeploymentEnvironment = Environment.GetEnvironmentVariable("ENV");
            this.ConsulAddress = Environment.GetEnvironmentVariable("CONSUL");
            this.ConfigRoot = Environment.GetEnvironmentVariable("GIGYA_CONFIG_ROOT")?.To(x => new DirectoryInfo(x));
            this.LoadPathsFile = Environment.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE")?.To(x => new FileInfo(x));

            var d = new Dictionary<string, string>();

            d.Add("DC", Environment.GetEnvironmentVariable("DC"));
            d.Add("ENV", Environment.GetEnvironmentVariable("ENV"));
            d.Add("GIGYA_CONFIG_ROOT", Environment.GetEnvironmentVariable("GIGYA_CONFIG_ROOT"));
            d.Add("GIGYA_BASE_PATH", Environment.GetEnvironmentVariable("GIGYA_BASE_PATH"));

            this.CustomKeys = d;
        }
    }
}
