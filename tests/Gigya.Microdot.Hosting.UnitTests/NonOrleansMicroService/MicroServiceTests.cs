using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Service;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Service;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    [TestFixture]
    public class MicroServiceTests
    {
        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", AppDomain.CurrentDomain.BaseDirectory, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("REGION", "us1", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ZONE", "us1a", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ENV", "_Test", EnvironmentVariableTarget.Process);
        }
        [Test]
        public async Task ShouldCallSelfHostServcie()
        {
            NonOrleansServiceTester<CalculatorServiceHost> serviceTester = null;
            var testingKernel = new TestingKernel<TraceLog>();
            try
            {
                serviceTester =
                    testingKernel.GetServiceTesterForNonOrleansService<CalculatorServiceHost>(1111,
                        TimeSpan.FromSeconds(10));
                (await serviceTester.GetServiceProxy<ICalculatorService>().Add(1, 2)).ShouldBe(3);
            }
            finally
            {
                serviceTester?.Dispose();
                testingKernel.Dispose();
            }


        }

    }
}