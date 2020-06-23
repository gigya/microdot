using Gigya.Microdot.LanguageExtensions;
using Gigya.Microdot.SharedLogic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gigya.Microdot.Hosting.Environment
{

    public sealed class EnvironmentVarialbesConfigurationSource : IHostEnvironmentSource
    {
        public string Zone { get; }

        public string Region { get; }

        public string DeploymentEnvironment { get; }

        public string ConsulAddress { get; }

        public string InstanceName { get; }

        public CurrentApplicationInfo ApplicationInfo { get; }

        public DirectoryInfo ConfigRoot { get; }

        public FileInfo LoadPathsFile { get; }

        public IDictionary<string, string> CustomVariables { get; }

        public EnvironmentVarialbesConfigurationSource()
        {
            this.ApplicationInfo = null;

            this.Zone = System.Environment.GetEnvironmentVariable("ZONE") ?? System.Environment.GetEnvironmentVariable("DC");
            this.Region = System.Environment.GetEnvironmentVariable("REGION");
            this.DeploymentEnvironment = System.Environment.GetEnvironmentVariable("ENV");
            this.ConsulAddress = System.Environment.GetEnvironmentVariable("CONSUL");
            this.InstanceName = System.Environment.GetEnvironmentVariable("GIGYA_SERVICE_INSTANCE_NAME");
            this.ConfigRoot = System.Environment.GetEnvironmentVariable("GIGYA_CONFIG_ROOT")?.To(x => new DirectoryInfo(x));
            this.LoadPathsFile = System.Environment.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE")?.To(x => new FileInfo(x));

            var d = new Dictionary<string, string>();

            d.Add("DC", System.Environment.GetEnvironmentVariable("DC"));
            d.Add("ENV", System.Environment.GetEnvironmentVariable("ENV"));
            d.Add("GIGYA_CONFIG_ROOT", System.Environment.GetEnvironmentVariable("GIGYA_CONFIG_ROOT"));
            d.Add("GIGYA_BASE_PATH", System.Environment.GetEnvironmentVariable("GIGYA_BASE_PATH"));

            this.CustomVariables = d;
        }
    }
}
