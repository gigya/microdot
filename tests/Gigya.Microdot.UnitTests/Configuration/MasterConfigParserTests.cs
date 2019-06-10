using System;
using System.Collections.Generic;
using System.Linq;

using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;

using NSubstitute;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public class MasterConfigParserTests
    {
        private IFileSystem _fileSystem;
        private IEnvironmentVariableProvider environmentVariableProvider;

        private const string env = "env1";
        private const string zone = "dc1";
        private const string testData =
@"//$(prefix) is a root folder c:\ or \etc
    [
        {Pattern: '$(prefix)/Gigya/Config/*.config',                       Priority:  2, SearchOption: 'TopDirectoryOnly' },
        {Pattern: '$(prefix)/Gigya/Config/$(appName)/*.config',            Priority:  3, SearchOption: 'TopDirectoryOnly' },
        {Pattern: '$(prefix)/Gigya/Config/%ENV%/*.config',                 Priority:  4, SearchOption: 'TopDirectoryOnly' },
        {Pattern: '$(prefix)/Gigya/Config/%ZONE%/*.config',                  Priority:  5, SearchOption: 'TopDirectoryOnly' },
        {Pattern: '$(prefix)/Gigya/Config/%ZONE%/$(appName)/*.config',       Priority:  6, SearchOption: 'TopDirectoryOnly' },
        {Pattern: '$(prefix)/Gigya/Config/%ZONE%/%ENV%/*.config',            Priority:  7, SearchOption: 'TopDirectoryOnly' },
        {Pattern: '$(prefix)/Gigya/Config/%ZONE%/%ENV%/$(appName)/*.config', Priority:  8, SearchOption: 'TopDirectoryOnly' },                    
        {Pattern: '$(prefix)/Gigya/Config/_local/*.config',                Priority: 9,  SearchOption: 'TopDirectoryOnly' },
        {Pattern: './Config/*.config',                                     Priority: 10, SearchOption: 'AllDirectories' }
    ]";


        [SetUp]
        public void SetUp()
        {
            _fileSystem = Substitute.For<IFileSystem>();
            _fileSystem.ReadAllTextFromFile(Arg.Any<string>()).Returns(a => testData);
            _fileSystem.Exists(Arg.Any<string>()).Returns(a => true);

            environmentVariableProvider = Substitute.For<IEnvironmentVariableProvider>();
            environmentVariableProvider.PlatformSpecificPathPrefix.Returns("c:");

        }


        [Test]
        public void AllPathExists_AllEnvironmentVariablesExists_EnvironmentExceptionExpected()
        {
            var expected = new[] {
                new ConfigFileDeclaration {Pattern = $"./Config/*.config", Priority = 10},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/_local/*.config", Priority = 9},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/{zone}/{env}/{""}/*.config", Priority = 8},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/{zone}/{env}/*.config", Priority = 7},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/{zone}/{""}/*.config", Priority = 6},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/{zone}/*.config", Priority = 5},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/{env}/*.config", Priority = 4},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/{""}/*.config", Priority = 3},
                new ConfigFileDeclaration {Pattern = $"c:/Gigya/Config/*.config", Priority = 2}
            };

           
            BaseTest(new Dictionary<string, string>  {
                {"ENV", env},
                {"ZONE", zone}
            }, expected);
        }


        [Test]
        public void AllPathExists_NoEnvironmentVariablesExists_EnvironmentExceptionExpected()
        {
            Action act = () => BaseTest(new Dictionary<string, string>(), new ConfigFileDeclaration[0]);

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
            Action act = () => new ConfigurationLocationsParser(_fileSystem, environmentVariableProvider, new CurrentApplicationInfo(""));
            
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
            Action act = () => new ConfigurationLocationsParser(_fileSystem, environmentVariableProvider, new CurrentApplicationInfo(""));

            act.ShouldThrow<EnvironmentException>()
                .Message.ShouldContain("some configurations lines have duplicate priorities");
        }

        public void BaseTest(Dictionary<string, string>  envDictionary, ConfigFileDeclaration[] expected)
        {

            environmentVariableProvider.GetEnvironmentVariable(Arg.Any<string>()).Returns(key =>  {
                envDictionary.TryGetValue(key.Arg<string>(), out string val);
                return val;
                                                                         });

            var configs = new ConfigurationLocationsParser(_fileSystem, environmentVariableProvider, new CurrentApplicationInfo(""));
            configs.ConfigFileDeclarations.Count.ShouldBe(expected.Length);

            foreach (var pair in configs.ConfigFileDeclarations.Zip(expected, (first, second) => new { first, second }))
            {
                pair.first.Pattern.ShouldBe(pair.second.Pattern);
                pair.first.Priority.ShouldBe(pair.second.Priority);
            }
        }
    }
}
