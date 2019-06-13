using Gigya.Microdot.Hosting.Metrics;
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
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", AppDomain.CurrentDomain.BaseDirectory, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("REGION", "us1", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ZONE", "us1a", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ENV", "_Test", EnvironmentVariableTarget.Process);
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

            var x = new CalculatorServiceHost();
            await Task.Run(() => x.Run(serviceArguments));
            var canaryType = typeof(MetricsConfiguration);

            Console.SetOut(prOut);

            Regex.IsMatch(buffer.ToString(), $"(OK|ERROR).*{canaryType.FullName}")
                .ShouldBeTrue("Output should contain a row with validation of the type");

            Console.WriteLine(buffer);
        }
    }
}