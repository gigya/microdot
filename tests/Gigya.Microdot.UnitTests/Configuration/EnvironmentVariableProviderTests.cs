using System;
using System.Collections.Generic;

using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Exceptions;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public class EnvironmentVariableProviderTests
    {
        private const string DEFAULT_REGION = "default_region";
        private const string DEFAULT_DC = "default_dc";
        private const string DEFAULT_ENV = "default_env";

        private IFileSystem _fileSystem;
        private string _originalENV;
        private string _originalREGION;
        private string _originalDC;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = Substitute.For<IFileSystem>();

            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => @"{
                REGION: 'il1',
                DC: 'il1a',
	            ENV: 'orl11',	
	            GIGYA_CONFIG_PATHS_FILE: 'C:\\gigya\\Config\\loadPaths1.json',
            }");

            _originalREGION = Environment.GetEnvironmentVariable("REGION");
            _originalDC = Environment.GetEnvironmentVariable("DC");
            _originalENV = Environment.GetEnvironmentVariable("ENV");
            Environment.SetEnvironmentVariable("DC", DEFAULT_DC);
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
            Environment.SetEnvironmentVariable("DC", _originalDC);
            Environment.SetEnvironmentVariable("REGION", _originalREGION);
            Environment.SetEnvironmentVariable("ENV", _originalENV);
        }

        [Test]
        public void ReadsEnvFromDifferentFile()
        {
            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", "C:\\gigya\\envVars.json");
            new EnvironmentVariableProvider(_fileSystem);

            _fileSystem.Received().TryReadAllTextFromFile("c:\\gigya\\envvars.json");
        }

        [Test]
        public void ReadsEnvFromDefaultFile()
        {
            var environmentVariableProvider = new EnvironmentVariableProvider(_fileSystem);

            _fileSystem.Received().TryReadAllTextFromFile(environmentVariableProvider.PlatformSpecificPathPrefix + "/gigya/environmentVariables.json");
        }

        [Test]
        public void ReadAndSeEnvVariables_SomeEmpty()
        {
            Environment.SetEnvironmentVariable("DC", "il2");

            var environmentVariableProvider = new EnvironmentVariableProvider(_fileSystem);

            environmentVariableProvider.GetEnvironmentVariable("DC").ShouldBe("il1a");
            environmentVariableProvider.GetEnvironmentVariable("REGION").ShouldBe("il1");
            environmentVariableProvider.GetEnvironmentVariable("ENV").ShouldBe("orl11");
            environmentVariableProvider.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE").ShouldBe("c:\\gigya\\config\\loadpaths1.json");
        }


        [Test]
        public void ReadAndSeEnvVariables_AllEmpty()
        {
            var environmentVariableProvider = new EnvironmentVariableProvider(_fileSystem);

            environmentVariableProvider.GetEnvironmentVariable("DC").ShouldBe("il1a");
            environmentVariableProvider.GetEnvironmentVariable("REGION").ShouldBe("il1");
            environmentVariableProvider.GetEnvironmentVariable("ENV").ShouldBe("orl11");
            environmentVariableProvider.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE").ShouldBe("c:\\gigya\\config\\loadpaths1.json");
        }

        [Test]
        public void OnNotExistingFile_DoNothing()
        {
            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => null);

            var environmentVariableProvider = new EnvironmentVariableProvider(_fileSystem);

            // assert environment variables were not changed
            environmentVariableProvider.GetEnvironmentVariable("DC").ShouldBe(DEFAULT_DC);
            environmentVariableProvider.GetEnvironmentVariable("REGION").ShouldBe(DEFAULT_REGION);
            environmentVariableProvider.GetEnvironmentVariable("ENV").ShouldBe(DEFAULT_ENV);
        }


        [Test]
        public void OnFileParsingFailure_DoNothing()
        {
            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => @"Invalid JSON file");

            Action doAction = () =>
                              {
                                  new EnvironmentVariableProvider(_fileSystem);
                              };
            doAction.ShouldThrow<ConfigurationException>();
        }
    }
}
