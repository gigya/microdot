using Gigya.Microdot.Hosting.Environment;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gigya.Microdot.UnitTests.Configuration
{
    /// <summary>
    /// This test is modifying the environment variables it must not run in parallel with other tests!
    /// </summary>
    [TestFixture, Parallelizable(ParallelScope.None)]
    public class HostConfigurationSources
    {
        private Dictionary<string,string> envs = new Dictionary<string, string>
        {
            {"ENV",null},
            {"DC",null},
            {"GIGYA_CONFIG_PATHS_FILE",null},
            {"GIGYA_CONFIG_ROOT",null},
            {"Consul",null},
        };

        [SetUp]
        public void Setup()
        {
            //storing old values
            foreach (var key in envs.Keys.ToList())
            {
                envs[key] = Environment.GetEnvironmentVariable(key);
            }
        }

        [TearDown]
        public void TearDown()
        {
            //restoring old values
            foreach (var keyValue in envs)
            {
                Environment.SetEnvironmentVariable(keyValue.Key, keyValue.Value);
            }
        }

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
