using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.Metrics;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using NUnit.Framework;
using Shouldly;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class MicroServiceTests
    {
        [SetUp]
        public void Setup()
        {
            //Environment.SetEnvironmentVariable("REGION", "us1", EnvironmentVariableTarget.Process);
            //Environment.SetEnvironmentVariable("ZONE", "us1a", EnvironmentVariableTarget.Process);
            //Environment.SetEnvironmentVariable("ENV", "_Test", EnvironmentVariableTarget.Process);
        }

        [TearDown]
        public void TearDown()
        {
            //Environment.SetEnvironmentVariable("REGION", null);
            //Environment.SetEnvironmentVariable("ZONE", null);
            //Environment.SetEnvironmentVariable("ENV", null);
        }

        [Test]
        public async Task RunInConfigurationVerification_ShouldWriteResults()
        {
            var buffer = new StringBuilder();
            var prOut = Console.Out;
            Console.SetOut(new StringWriter(buffer));
            //ServiceStartupMode.VerifyConfigurations
            //ConsoleOutputMode.Standard
            var serviceArguments = new ServiceArguments(ServiceStartupMode.VerifyConfigurations, ConsoleOutputMode.Standard, SiloClusterMode.PrimaryNode, 8555);

            var config = new HostEnvironment(
                new TestHostEnvironmentSource(
                    region: "us1",
                    zone: "zone",
                    deploymentEnvironment: "_Test",
                    appName: "ICalculatorService"));

            var x = new 
                CalculatorServiceHost(config);

            await Task.Run(() => x.Run(serviceArguments));
            var canaryType = typeof(MetricsConfiguration);

            Console.SetOut(prOut);

            Regex.IsMatch(buffer.ToString(), $"(OK|ERROR).*{canaryType.FullName}")
                .ShouldBeTrue("Output should contain a row with validation of the type");

            Console.WriteLine(buffer);
        }
    }
}