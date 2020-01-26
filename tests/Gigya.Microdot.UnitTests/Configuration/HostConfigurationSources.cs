using Gigya.Microdot.Configuration;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public class HostConfigurationSources
    {
        [Test]
        public void LegacyConfigSource_HappyPath()
        {
            var file = Path.GetTempFileName();

            try
            {
                File.WriteAllText(
                    file,
                    @"{
                      ""ENV"": ""prod"",
                      ""DC"": ""il1"",
                      ""GIGYA_CONFIG_PATHS_FILE"": ""C:/Dev/Gigya-Config/loadPathsWithLocal.json"",
                      ""GIGYA_CONFIG_ROOT"": ""C:/Dev/Gigya-Config"",
                      ""Consul"": ""consul.localhost:8500""
                    }");

                var source = new LegacyFileHostConfigurationSource(file);

                Assert.AreEqual("prod", source.DeploymentEnvironment);
                Assert.AreEqual("il1", source.Zone);
                Assert.AreEqual("C:\\Dev\\Gigya-Config", source.ConfigRoot.FullName);
                Assert.AreEqual("C:\\Dev\\Gigya-Config\\loadPathsWithLocal.json", source.LoadPathsFile.FullName);
                Assert.AreEqual("consul.localhost:8500", source.ConsulAddress);
            }
            
            finally
            {
                File.Delete(file);
            }
        }
    }
}
