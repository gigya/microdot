using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.UnitTests.Caching.Host;
using Ninject;
using NSubstitute;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public abstract class ConfigTestBase
    {
        protected string LoadPaths { get; set; } = @"[{ ""Pattern"": "".\\*.config"", ""Priority"": 1 }]";

        /// <summary>
        /// Initial common kernel setup for mocks
        /// </summary>
        protected virtual (StandardKernel k, IAssemblyProvider providerMock, IFileSystem fileSystemMock) Setup()
        {
            var k = new StandardKernel();
            k.Load(new ConfigVerificationModule(new ConsoleLogLoggersModules(), new ServiceArguments(), "InfraTests", infraVersion: null));

            var cfg =
                new HostEnvironment(
                    new TestHostEnvironmentSource(
                        "InfraTests"));


            k.Bind<IEnvironment>().ToConstant(cfg);
            k.Bind<CurrentApplicationInfo>().ToConstant(cfg.ApplicationInfo);

            IAssemblyProvider providerMock = Substitute.For<IAssemblyProvider>();
            providerMock.GetAssemblies().Returns(info => new[] { GetType().Assembly });

            IFileSystem fileSystemMock = Substitute.For<IFileSystem>();
            fileSystemMock.ReadAllTextFromFile(Arg.Any<string>()).Returns(a => LoadPaths);
            fileSystemMock.Exists(Arg.Any<string>()).Returns(a => true);

            k.Rebind<IAssemblyProvider>().ToConstant(providerMock);
            k.Rebind<IFileSystem>().ToConstant(fileSystemMock);

            return (k, providerMock, fileSystemMock);
        }
    }

    [ConfigurationRoot("StringArrayConfig", RootStrategy.ReplaceClassNameWithPath)]
    internal class StringArrayConfig : IConfigObject
    {
        public string[] StringArray { get; set; }
    }

    [ConfigurationRoot("IEnumerableConfig", RootStrategy.ReplaceClassNameWithPath)]
    internal class IEnumerableConfig : IConfigObject
    {
        public IEnumerable<int> IntEnumerable { get; set; }
    }

    [ConfigurationRoot("IntArrayConfig", RootStrategy.ReplaceClassNameWithPath)]
    internal class IntArrayConfig : IConfigObject
    {
        public int[] IntArray { get; set; }
    }

    [ConfigurationRoot("PersonArrayConfig", RootStrategy.ReplaceClassNameWithPath)]
    internal class PersonArrayConfig : IConfigObject
    {
        public Person[] PersonArray { get; set; }
    }

    internal class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public Pet[] Pets { get; set; }
        public int[] FavoriteLotteryNumbers { get; set; }
    }

    internal class Pet
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
    [ConfigurationRoot("NestedConfig", RootStrategy.ReplaceClassNameWithPath)]
    internal class NestedConfig : IConfigObject
    {
        public InternalConfig[] Internals { get; set; }
    }

    internal class InternalConfig
    {
        public string Value { get; set; }
    }
}
