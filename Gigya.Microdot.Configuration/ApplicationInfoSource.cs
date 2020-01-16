using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using System.IO;

namespace Gigya.Microdot.Configuration
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

        public ApplicationInfoSource(CurrentApplicationInfo applicationInfo)
        {
            this.ApplicationInfo = applicationInfo;
        }
    }
}
