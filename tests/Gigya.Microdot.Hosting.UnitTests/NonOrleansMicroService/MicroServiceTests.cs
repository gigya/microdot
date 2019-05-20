using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Metrics;
using Gigya.Microdot.SharedLogic;
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
                    testingKernel.GetServiceTesterForNonOrleansService<CalculatorServiceHost>(1111);
                (await serviceTester.GetServiceProxy<ICalculatorService>().Add(1, 2)).ShouldBe(3);
            }
            finally
            {
                serviceTester?.Dispose();
                testingKernel.Dispose();
            }
        }

        [Test]
        public async Task RunInConfigurationVerification_ShouldWriteResults()
        {

            NonOrleansServiceTester<CalculatorServiceHost> serviceTester = null;
            var testingKernel = new TestingKernel<ConsoleLog>();

            try
            {
                var buffer = new StringBuilder();
                var prOut = Console.Out;
                Console.SetOut(new StringWriter(buffer));
                ///ServiceStartupMode.VerifyConfigurations: 0 
                var serviceArguments = new ServiceArguments(ServiceStartupMode.VerifyConfigurations, ConsoleOutputMode.Disabled,
                    SiloClusterMode.PrimaryNode, 8555);

                serviceTester =
                    testingKernel.GetServiceTesterForNonOrleansService<CalculatorServiceHost>(serviceArguments);

                var canaryType = typeof(MetricsConfiguration);

                Console.SetOut(prOut);

                Regex.IsMatch(buffer.ToString(), $"(OK|ERROR).*{canaryType.FullName}")
                    .ShouldBeTrue("Output should contain a row with validation of the type");

                Console.WriteLine(buffer);

            }
            finally
            {
                Should.Throw<InvalidOperationException>(() => serviceTester?.Dispose())
                                                    .Message.ShouldContain("Service is already stopped");
                testingKernel.Dispose();
            }
        }
    }
}