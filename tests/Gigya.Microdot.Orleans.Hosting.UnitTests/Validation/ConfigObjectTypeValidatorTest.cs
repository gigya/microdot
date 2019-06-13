using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Validation
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class ConfigObjectTypeValidatorTest
    {
        [Test]
        public void ThrowExceptionWhenValueTypeImplementsIConfigObject()
        {
            IAssemblyProvider assemblyProviderMock = Substitute.For<IAssemblyProvider>();
            assemblyProviderMock.GetAllTypes().Returns(new [] {typeof(ReferenceTypeConfig), typeof(ValueTypeConfig) });

            ConfigObjectTypeValidator configValidator = new ConfigObjectTypeValidator(assemblyProviderMock);

            Assert.Throws<ProgrammaticException>(configValidator.Validate);
        }

        [Test]
        public void NoValueTypesInAssemblies_TestPassed()
        {
            IAssemblyProvider assemblyProviderMock = Substitute.For<IAssemblyProvider>();
            assemblyProviderMock.GetAllTypes().Returns(new[] { typeof(ReferenceTypeConfig) });

            ConfigObjectTypeValidator configValidator = new ConfigObjectTypeValidator(assemblyProviderMock);

            configValidator.Validate();
        }
    }

    public struct ValueTypeConfig : IConfigObject
    { }

    public class ReferenceTypeConfig : IConfigObject
    { }
}
