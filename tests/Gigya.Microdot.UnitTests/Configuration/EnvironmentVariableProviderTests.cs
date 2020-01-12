using System;
using System.Configuration;
using System.IO;
using System.Linq;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Exceptions;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public class EnvironmentVariableProviderTests
    {
        private const string DEFAULT_REGION = "default_region";
        private const string DEFAULT_ZONE = "default_zone";
        private const string DEFAULT_ENV = "default_env";

        private string _originalENV;
        private string _originalREGION;
        private string _originalZone;

        [SetUp]
        public void SetUp()
        {
            _originalREGION = Environment.GetEnvironmentVariable("REGION");
            _originalZone = Environment.GetEnvironmentVariable("ZONE");
            _originalENV = Environment.GetEnvironmentVariable("ENV");
            Environment.SetEnvironmentVariable("ZONE", DEFAULT_ZONE);
            Environment.SetEnvironmentVariable("REGION", DEFAULT_REGION);
            Environment.SetEnvironmentVariable("ENV", DEFAULT_ENV);
            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", null);
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", null);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", null);
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", null);
            Environment.SetEnvironmentVariable("ZONE", _originalZone);
            Environment.SetEnvironmentVariable("REGION", _originalREGION);
            Environment.SetEnvironmentVariable("ENV", _originalENV);
        }

        [Test]
        public void ReadsEnvFromFile()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, @"{
                    REGION: 'il1',
                    ZONE: 'il1a',
	                ENV: 'orl11',	
	                GIGYA_CONFIG_PATHS_FILE: 'C:\\gigya\\Config\\loadPaths1.json',
                }");
                
                var entries = EnvironmentVariables.ReadFromJsonFile(path);

                Assert.IsTrue(
                    Enumerable.SequenceEqual(
                        entries.Select(x => (x.Key, x.Value)),
                        new[] {
                        ("REGION", "il1"),
                        ("ZONE", "il1a"),
                        ("ENV", "orl11"),
                        ("GIGYA_CONFIG_PATHS_FILE", "C:\\gigya\\Config\\loadPaths1.json"),
                        }));
            }

            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void OnFileParsingFailure_DoNothing()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, @"invalid file");


                Action doAction = () =>
                {
                    EnvironmentVariables.ReadFromJsonFile(path);
                };

                doAction.ShouldThrow<ConfigurationErrorsException>();
            }

            finally
            {
                File.Delete(path);
            }
        }
    }
}
