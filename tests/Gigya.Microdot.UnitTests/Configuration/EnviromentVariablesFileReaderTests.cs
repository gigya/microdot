using System;
using System.Collections.Generic;

using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Exceptions;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public class EnviromentVariablesFileReaderTests
    {
        private IFileSystem _fileSystem;
        private IEnvironment enviroment;
        private Dictionary<string, string> envVariables;

        [SetUp]
        public void SetUp()
        {
            envVariables = new Dictionary<string, string>();

            _fileSystem = Substitute.For<IFileSystem>();

            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => @"{
                DC: 'il11',
	            ENV: 'orl11',	
	            GIGYA_CONFIG_PATHS_FILE: 'C:\\gigya\\Config\\loadPaths1.json',
            }");

            enviroment = Substitute.For<IEnvironment>();            
            enviroment.GetEnvironmentVariable(Arg.Any<string>()).Returns(a =>
            {
                string val;
                envVariables.TryGetValue(a.Arg<string>(), out val);
                return val;
            });
            enviroment.When(a => a.SetEnvironmentVariableForProcess(Arg.Any<string>(), Arg.Any<string>())).Do(a =>
            {
                envVariables[a.ArgAt<string>(0)] = a.ArgAt<string>(1);
            });
        }

        [Test]
        public void ReadsEnvFromDifferentFile()
        {
            envVariables = new Dictionary<string, string>{                
                {"GIGYA_ENVVARS_FILE", "C:\\gigya\\envVars.json"}
            };


            var reader = new EnvironmentVariablesFileReader(_fileSystem, enviroment);
            reader.ReadFromFile();

           
            _fileSystem.Received().TryReadAllTextFromFile("C:\\gigya\\envVars.json");
        }

        [Test]
        public void ReadsEnvFromDefaultFile()
        {
            var envFilePath = enviroment.PlatformSpecificPathPrefix + "/gigya/environmentVariables.json";

            var reader = new EnvironmentVariablesFileReader(_fileSystem, enviroment);
            reader.ReadFromFile();


            _fileSystem.Received().TryReadAllTextFromFile(envFilePath);
        }

        [Test]
        public void ReadAndSeEnvVariables_SomeEmpty()
        {
            envVariables = new Dictionary<string, string> {{"DC", "il2"}};
           
            var reader = new EnvironmentVariablesFileReader(_fileSystem, enviroment);
            reader.ReadFromFile();
            
            enviroment.GetEnvironmentVariable("DC").ShouldBe("il11");
            enviroment.GetEnvironmentVariable("ENV").ShouldBe("orl11");
            enviroment.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE").ShouldBe("C:\\gigya\\Config\\loadPaths1.json");
        }


        [Test]
        public void ReadAndSeEnvVariables_AllEmpty()
        {

            envVariables = new Dictionary<string, string>();

            var reader = new EnvironmentVariablesFileReader(_fileSystem, enviroment);
            reader.ReadFromFile();

            enviroment.GetEnvironmentVariable("DC").ShouldBe("il11");
            enviroment.GetEnvironmentVariable("ENV").ShouldBe("orl11");
            enviroment.GetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE").ShouldBe("C:\\gigya\\Config\\loadPaths1.json");
        }

        [Test]
        public void OnNotExistingFile_DoNothing()
        {

            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => null);
                       
            var reader = new EnvironmentVariablesFileReader(_fileSystem, enviroment);
            reader.ReadFromFile();

            enviroment.DidNotReceiveWithAnyArgs().WhenForAnyArgs(a => a.SetEnvironmentVariableForProcess(Arg.Any<string>(), Arg.Any<string>()));
        }


        [Test]
        public void OnFileParsingFailure_DoNothing()
        {
            _fileSystem.TryReadAllTextFromFile(Arg.Any<string>()).Returns(a => @"Invalid JSON file");

            Action doAction = () =>
                              {
                                  var reader = new EnvironmentVariablesFileReader(_fileSystem, enviroment);
                                  reader.ReadFromFile();
                              };
            doAction.ShouldThrow<ConfigurationException>();
            enviroment.DidNotReceiveWithAnyArgs().WhenForAnyArgs(a => a.SetEnvironmentVariableForProcess(Arg.Any<string>(), Arg.Any<string>()));
        }
    }
}
