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
    public class EnviromentVariableProviderTests
    {
        private const string DEFAULT_DC = "default_dc";
        private const string DEFAULT_ENV = "default_env";

        private IFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = Substitute.For<IFileSystem>();

            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => @"{
                DC: 'il11',
	            ENV: 'orl11',	
	            GIGYA_CONFIG_PATHS_FILE: 'C:\\gigya\\Config\\loadPaths1.json',
            }");

            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", null);
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", null);
            Environment.SetEnvironmentVariable("DC", DEFAULT_DC);
            Environment.SetEnvironmentVariable("ENV", DEFAULT_ENV);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", null);
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", null);
            Environment.SetEnvironmentVariable("DC", null);
            Environment.SetEnvironmentVariable("ENV", null);
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

            var enviromentVariableProvider = new EnvironmentVariableProvider(_fileSystem);

            enviromentVariableProvider.GetEnvironmentVariable("DC").ShouldBe("il11");
            enviromentVariableProvider.GetEnvironmentVariable("ENV").ShouldBe("orl11");
            enviromentVariableProvider.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE").ShouldBe("c:\\gigya\\config\\loadpaths1.json");
        }


        [Test]
        public void ReadAndSeEnvVariables_AllEmpty()
        {
            var enviromentVariableProvider = new EnvironmentVariableProvider(_fileSystem);

            enviromentVariableProvider.GetEnvironmentVariable("DC").ShouldBe("il11");
            enviromentVariableProvider.GetEnvironmentVariable("ENV").ShouldBe("orl11");
            enviromentVariableProvider.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE").ShouldBe("c:\\gigya\\config\\loadpaths1.json");
        }

        [Test]
        public void OnNotExistingFile_DoNothing()
        {
            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => null);

            var environmentVariableProvider = new EnvironmentVariableProvider(_fileSystem);

            // assert environment variables were not changed
            environmentVariableProvider.GetEnvironmentVariable("DC").ShouldBe(DEFAULT_DC);
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
