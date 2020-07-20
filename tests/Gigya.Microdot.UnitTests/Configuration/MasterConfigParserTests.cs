using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using NSubstitute;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    [NonParallelizable]
    public class MasterConfigParserTests
    {
        private IFileSystem _fileSystem;

        private static string env = Environment.GetEnvironmentVariable("ENV");
        private static string zone = Environment.GetEnvironmentVariable("ZONE");

        private const string testData =
@"
    [
        {Pattern: 'Gigya/Config/*.config',                       Priority:  2, SearchOption: 'TopDirectoryOnly' },
        {Pattern: 'Gigya/Config/$(appName)/*.config',            Priority:  3, SearchOption: 'TopDirectoryOnly' },
        {Pattern: 'Gigya/Config/%ENV%/*.config',                 Priority:  4, SearchOption: 'TopDirectoryOnly' },
        {Pattern: 'Gigya/Config/%ZONE%/*.config',                  Priority:  5, SearchOption: 'TopDirectoryOnly' },
        {Pattern: 'Gigya/Config/%ZONE%/$(appName)/*.config',       Priority:  6, SearchOption: 'TopDirectoryOnly' },
        {Pattern: 'Gigya/Config/%ZONE%/%ENV%/*.config',            Priority:  7, SearchOption: 'TopDirectoryOnly' },
        {Pattern: 'Gigya/Config/%ZONE%/%ENV%/$(appName)/*.config', Priority:  8, SearchOption: 'TopDirectoryOnly' },                    
        {Pattern: 'Gigya/Config/_local/*.config',                Priority: 9,  SearchOption: 'TopDirectoryOnly' },
        {Pattern: './Config/*.config',                                     Priority: 10, SearchOption: 'AllDirectories' }
    ]";


        [SetUp]
        public void SetUp()
        {
            _fileSystem = Substitute.For<IFileSystem>();
            _fileSystem.ReadAllTextFromFile(Arg.Any<string>()).Returns(a => testData);
            _fileSystem.Exists(Arg.Any<string>()).Returns(a => true);
        }


        [Test]
        public void AllPathExists_AllEnvironmentVariablesExists_EnvironmentExceptionExpected()
        {
            var expected = new[] {
                new ConfigFileDeclaration {Pattern = $"./Config/*.config", Priority = 10},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/_local/*.config", Priority = 9},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/{zone}/{env}/{"test"}/*.config", Priority = 8},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/{zone}/{env}/*.config", Priority = 7},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/{zone}/{"test"}/*.config", Priority = 6},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/{zone}/*.config", Priority = 5},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/{env}/*.config", Priority = 4},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/{"test"}/*.config", Priority = 3},
                new ConfigFileDeclaration {Pattern = $"Gigya/Config/*.config", Priority = 2}
            };

           
            BaseTest(new Dictionary<string, string>  {
                {"ENV", env},
                {"ZONE", zone}
            }, expected);
        }


        [Test]
        [Ignore("Test this in new config system.")]
        public void AllPathExists_NoEnvironmentVariablesExists_EnvironmentExceptionExpected()
        {
            Action act = () => BaseTest(new Dictionary<string, string> { { "ENV", null }, { "ZONE", null } }, new ConfigFileDeclaration[0]);

            act.ShouldThrow<EnvironmentException>()
               .Message.ShouldContain("Some environment variables are not defined, please add them");
        }

        [Test]
        [TestCase("Not a JSON", TestName = "Not a JSON file")]
        [TestCase(@"[{Pattern: './*.config', Priority:  1 }", TestName = "Missing ]")]        
        [TestCase(@"[{Priority:  1 }]", TestName = "Missing Pattern")]
        [TestCase(@"[{Pattern: './*.config' }]", TestName = "Missing Priority")]        
        [TestCase(@"[{Pattern: {}, Priority:  1 }]", TestName = "Invalid Pattern")]
        [TestCase(@"[{Pattern: './*.config', Priority:  null }]", TestName = "Invalid Priority")]
        public void FileFormatIsInvalid_ShouldThrowEnvironmentException(string testData)
        {
            _fileSystem.ReadAllTextFromFile(Arg.Any<string>()).Returns(a => testData);
            Action act = () => new ConfigurationLocationsParser(_fileSystem, new NullEnvironment(), new CurrentApplicationInfo("test", Environment.UserName, Dns.GetHostName()));
            
            act.ShouldThrow<EnvironmentException>()
                .Message.ShouldContain("Problem reading");
        }

        [Test]
        public void DuplicatePriority_ShouldThrowEnvironmentException()
        {

         var testData=
         @"[{Pattern: '$(prefix)/Gigya/Config/*.config',                       Priority:  1, SearchOption: 'TopDirectoryOnly' },
            {Pattern: '$(prefix)/Gigya/Config/$(appName)/*.config',            Priority:  1, SearchOption: 'TopDirectoryOnly' }]";

            _fileSystem.ReadAllTextFromFile(Arg.Any<string>()).Returns(a => testData);
            Action act = () => new ConfigurationLocationsParser(_fileSystem, new NullEnvironment(), new CurrentApplicationInfo("test", Environment.UserName, Dns.GetHostName()));

            act.ShouldThrow<EnvironmentException>()
                .Message.ShouldContain("some configurations lines have duplicate priorities");
        }

        public void BaseTest(Dictionary<string, string>  envDictionary, ConfigFileDeclaration[] expected)
        {
            var oldEnv = new Dictionary<string, string>();

            foreach (var e in envDictionary)
            {
                oldEnv[e.Key] = Environment.GetEnvironmentVariable(e.Key);
                Environment.SetEnvironmentVariable(e.Key, e.Value);
            }

            try
            {
                var config = new HostEnvironment(
                    new TestHostEnvironmentSource(),
                    new EnvironmentVarialbesConfigurationSource(),
                    new ApplicationInfoSource(
                        new CurrentApplicationInfo("test", Environment.UserName, Dns.GetHostName())));

                var configs = new ConfigurationLocationsParser(_fileSystem, config, config.ApplicationInfo);
                configs.ConfigFileDeclarations.Count.ShouldBe(expected.Length);

                foreach (var pair in configs.ConfigFileDeclarations.Zip(expected, (first, second) => new { first, second }))
                {
                    pair.first.Pattern.ShouldBe(pair.second.Pattern);
                    pair.first.Priority.ShouldBe(pair.second.Priority);
                }
            }

            finally
            {
                foreach (var e in oldEnv)
                    Environment.SetEnvironmentVariable(e.Key, e.Value);
            }
        }
    }
}
