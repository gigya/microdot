using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces;
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
}
