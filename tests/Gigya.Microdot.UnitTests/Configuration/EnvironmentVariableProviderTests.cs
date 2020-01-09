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

        private IFileSystem _fileSystem;
        private string _originalENV;
        private string _originalREGION;
        private string _originalZone;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = Substitute.For<IFileSystem>();

            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => @"{
                REGION: 'il1',
                ZONE: 'il1a',
	            ENV: 'orl11',	
	            GIGYA_CONFIG_PATHS_FILE: 'C:\\gigya\\Config\\loadPaths1.json',
            }");

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
            var path = "some/path";

            var entries = EnvironmentVariables.ReadFromFile(_fileSystem, path);

            Assert.IsTrue(
                Enumerable.SequenceEqual(
                    entries.Select(x => (x.Key, x.Value)),
                    new[] {
                        ("REGION", "il1"),
                        ("ZONE", "il1a"),
                        ("ENV", "orl11"),
                        ("GIGYA_CONFIG_PATHS_FILE", "C:\\gigya\\Config\\loadPaths1.json"),
                    }));
                
            _fileSystem.Received().TryReadAllTextFromFile(path);
        }

        [Test]
        public void OnFileParsingFailure_DoNothing()
        {
            var path = "some/path";

            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => @"Invalid JSON file");

            Action doAction = () => {
                EnvironmentVariables.ReadFromFile(_fileSystem, path);
            };

            doAction.ShouldThrow<ConfigurationErrorsException>();
        }
    }
}
