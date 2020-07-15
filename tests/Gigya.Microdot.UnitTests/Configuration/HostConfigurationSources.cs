using Gigya.Microdot.Hosting.Environment;
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;

namespace Gigya.Microdot.UnitTests.Configuration
{
    /// <summary>
    /// This test is modifying the environment variables it must not run in parallel with other tests!
    /// </summary>
    [TestFixture, Parallelizable(ParallelScope.None)]
    public class HostConfigurationSources
    {
        private IDictionary _envs;

        [SetUp]
        public void Setup()
        {
            _envs = Environment.GetEnvironmentVariables();
        }

        [TearDown]
        public void TearDown()
        {
            //Remove current variables
            foreach (DictionaryEntry keyPair in Environment.GetEnvironmentVariables())
            {
                Environment.SetEnvironmentVariable(keyPair.Key.ToString(),null);
            }

            //Restore old variables
            foreach (DictionaryEntry keyPair in _envs)
            {
                Environment.SetEnvironmentVariable(keyPair.Key.ToString(), keyPair.Value.ToString());
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
