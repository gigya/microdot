using Gigya.Microdot.SharedLogic;
using System.Collections.Generic;
using System.IO;

namespace Gigya.Microdot.Hosting.Environment
{
    public sealed class ApplicationInfoSource : IHostEnvironmentSource
    {
        public string Zone { get; }

        public string Region { get; }

        public string DeploymentEnvironment { get; }

        public string ConsulAddress { get; }

        public string InstanceName { get; }

        public CurrentApplicationInfo ApplicationInfo { get; }

        public DirectoryInfo ConfigRoot { get; }

        public FileInfo LoadPathsFile { get; }

        public IDictionary<string, string> EnvironmentVariables => new Dictionary<string, string>();

        public ApplicationInfoSource(CurrentApplicationInfo applicationInfo)
        {
            this.ApplicationInfo = applicationInfo;
        }
    }
}
