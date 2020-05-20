using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.UnitTests.Caching.Host;

using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration.Verificator
{
    /// <summary>
    /// The tests to ensure Verificator recognizing the configuration failures in major cases we expect
    /// </summary>
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class VerificatorTests: ConfigTestBase
    {
        [Test]
        [Description("check we recognize a broken XML file")]
        public void WhenConfigIsNotValidXmlShouldAddFailure()
        {
            var (k, providerMock, fileSystemMock) = Setup();


            providerMock.GetAllTypes().Returns(info => new[]
            {
                typeof(VerifiedConfig2),
            });

            fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new []
            {
                "VerifiedConfig2.config",
            });

            fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
            {
                string content;
                if (callinfo.ArgAt<string>(0) == "VerifiedConfig2.config")
                {
                    // required attribute is NOT satisfied !!!
                    content = @"<configuration with WRONG XML configuration>";
                }
                else
                    throw new ArgumentException("Invalid config file name...");

                return Task.FromResult(content);
            });

            var v = k.Get<ConfigurationVerificator>();

            var s = v.Verify();

            s.All(result => result.Success).ShouldBeFalse();

            // --->>>> CONFIGURATION OBJECTS FAILED TO PASS THE VERIFICATION <<<<-----
            // 	TYPE: Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig1
            // PATH :  Missing or invalid configuration file: VerifiedConfig1.config
            // ERROR:  Root element is missing.

            s.Any(failure => 
                failure.Type.Name == typeof(VerifiedConfig2).Name &&
                failure.Path.Contains("Missing or invalid configuration file")).ShouldBeTrue();

            foreach (var result in s)
                Console.WriteLine(result);
        }

        [Test]
        [Ignore("just to be able to search in tests tree")]
        public void VerificatorTest()
        {
        }

        [Test]
        [Description("check we recognize a violation of annotated property in config object")]
        public void WhenDataAnnotationViolatedShouldAddFailure()
        {
            var (k, providerMock, fileSystemMock) = Setup();

            providerMock.GetAllTypes().Returns(info => new[]
            {
                typeof(VerifiedConfig2),
            });

            fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new[]
            {
                "VerifiedConfig2.config",
            });

            fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
            {
                string content;
                if (callinfo.ArgAt<string>(0) == "VerifiedConfig2.config")
                {
                     // required attribute is NOT satisfied !!!
                     content = @"<configuration></configuration>";
                }
                else
                    throw new ArgumentException("Invalid config file name...");
                
                return Task.FromResult(content);
            });

            var v = k.Get<ConfigurationVerificator>();

            var s = v.Verify();

            // --->>>> CONFIGURATION OBJECTS FAILED TO PASS THE VERIFICATION <<<<-----
            // 	TYPE: Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig2
            // PATH :  VerifiedConfig2
            // ERROR:  The Required field is required.
            // 	The following 1 configuration objects passed the verification:
            // Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig1

            s.All(result => result.Success).ShouldBeFalse();

            s.All(failure =>
                failure.Type.Name == typeof(VerifiedConfig2).Name &&
                failure.Details.Contains("The Required field is required"))
            .ShouldBeTrue("Expected a failure! When no value is given for the property");

            foreach (var result in s)
                Console.WriteLine(result);
        }

        [Test]
        [Description("check we actually loading the value for property from File, not the default in class")]
        public void WhenValueLoadedFromConfigFileShouldSuccess()
        {
            var (k, providerMock, fileSystemMock) = Setup();

            providerMock.GetAllTypes().Returns(info => new[]
            {
                        typeof(VerifiedConfig1),
                    });

            fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new[]
            {
                        "VerifiedConfig1.config",
                    });

            fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
            {
                var content = "";
                if (callinfo.ArgAt<string>(0) == "VerifiedConfig1.config")
                {
                    content =
@"<configuration>
         <VerifiedConfig1>
            <ValueLoaded>theValue</ValueLoaded>
         </VerifiedConfig1>
         </configuration>";
                }
                return Task.FromResult(content);
            });

            var v = k.Get<ConfigurationVerificator>();

            var s = v.Verify();

            var creator = k.Get<Func<Type, IConfigObjectCreator>>()(typeof(VerifiedConfig1));
            ((VerifiedConfig1) creator.GetLatest()).ValueLoaded.ShouldBe("theValue");

            s.All(passed => passed.Type == typeof(VerifiedConfig1)).ShouldBeTrue();

            foreach (var result in s)
                Console.WriteLine(result);
        }

        [Test]
        [Description("check we recognize a case of a value is not converted into a another type")]
        public void WhenValueIsNotSuitableShouldAddFailure()
        {
            var (k, providerMock, fileSystemMock) = Setup();

            providerMock.GetAllTypes().Returns(info => new[]
            {
                typeof(VerifiedConfig3),
            });

            fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new[]
            {
                "VerifiedConfig3.config",
            });

            fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
            {
                string content;
                if (callinfo.ArgAt<string>(0) == "VerifiedConfig3.config")
                {
                    content =
                        @"<configuration>
         <VerifiedConfig3>
            <TheInt>theValue</TheInt>
         </VerifiedConfig3>
         </configuration>";
                }
                else
                    throw new ArgumentException("Invalid config file name...");

                return Task.FromResult(content);
            });


            var v = k.Get<ConfigurationVerificator>();

            var s = v.Verify();

            // --->>>> CONFIGURATION OBJECTS FAILED TO PASS THE VERIFICATION <<<<-----
            // 	TYPE: Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig3
            // PATH :  VerifiedConfig3
            // ERROR:  Failed to deserialize config object: Could not convert string to integer: theValue. Path 'TheInt'.


            s.All(result => result.Success).ShouldBeFalse();

            s.All(failure =>
                    failure.Type.Name == typeof(VerifiedConfig3).Name &&
                    failure.Details.Contains("Failed to deserialize config object"))
                .ShouldBeTrue("Test is expected to fail while string cannot be converted to an int.");

            foreach (var result in s)
                Console.WriteLine(result);
        }
    }
}
