using System.IO;
using System.Reflection;
using Gigya.Microdot.Interfaces;

namespace Gigya.Microdot.SharedLogic
{
    public class ApplicationDirectoryProvider : IApplicationDirectoryProvider
    {
        private string ApplicationDirectory { get; }

        public ApplicationDirectoryProvider(BaseCommonConfig commonConfig)
        {
            if (!string.IsNullOrWhiteSpace(commonConfig.ApplicationDirectoryOverride))
                ApplicationDirectory = commonConfig.ApplicationDirectoryOverride;
            else
                ApplicationDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // TODO: Remove this when all code uses IApplicationDirectoryProvider
            Directory.SetCurrentDirectory(ApplicationDirectory);
        }

        public string GetApplicationDirectory()
        {
            return ApplicationDirectory;
        }
    }
}
