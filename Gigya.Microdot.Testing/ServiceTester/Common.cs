using System;
using System.IO;
using System.Reflection;

namespace Gigya.Microdot.Testing.ServiceTester
{
    public class Common
    {
        public static AppDomain CreateDomain(string TestAppDomainName = "TestAppDomain")
        {
            AppDomain currentAppDomain = AppDomain.CurrentDomain;

            return AppDomain.CreateDomain(TestAppDomainName, null, new AppDomainSetup
            {
                ApplicationBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                ConfigurationFile = currentAppDomain.SetupInformation.ConfigurationFile,
                ShadowCopyFiles = currentAppDomain.SetupInformation.ShadowCopyFiles,
                ShadowCopyDirectories = currentAppDomain.SetupInformation.ShadowCopyDirectories,
                CachePath = currentAppDomain.SetupInformation.CachePath
            });
        }
    }
}