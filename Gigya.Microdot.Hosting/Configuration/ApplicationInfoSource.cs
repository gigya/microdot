using Gigya.Microdot.Interfaces.Configuration;
using System.Collections.Generic;
using System.IO;

namespace Gigya.Microdot.Hosting.Configuration
{
    public sealed class ApplicationInfoSource : IHostConfigurationSource
    {
        public string Zone { get; }

        public string Region { get; }

        public string DeploymentEnvironment { get; }

        public string ConsulAddress { get; }

        public CurrentApplicationInfo ApplicationInfo { get; }

        public DirectoryInfo ConfigRoot { get; }

        public FileInfo LoadPathsFile { get; }

        public IDictionary<string, string> CustomKeys => new Dictionary<string, string>();

        public ApplicationInfoSource(CurrentApplicationInfo applicationInfo)
        {
            this.ApplicationInfo = applicationInfo;
        }
    }
}
