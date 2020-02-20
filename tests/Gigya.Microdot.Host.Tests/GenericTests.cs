using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Host.Tests.Utils;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.Metrics;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Gigya.Microdot.Host.Tests
{
    public class GenericTests
    {
        [Fact]
        public async Task RunInConfigurationVerification_ShouldWriteResults()
        {
            var buffer = new StringBuilder();
            var prOut = Console.Out;
            Console.SetOut(new StringWriter(buffer));
            //ServiceStartupMode.VerifyConfigurations
            //ConsoleOutputMode.Standard
            var serviceArguments = new ServiceArguments(ServiceStartupMode.VerifyConfigurations, ConsoleOutputMode.Standard, SiloClusterMode.PrimaryNode, 8555);

            var config = new HostConfiguration(
                new TestHostConfigurationSource(
                    region: "us1",
                    zone: "zone",
                    deploymentEnvironment: "_Test"));

            var x = new Ninject.Host.Host(
                config,
                new CalculatorKernelConfig(),
                new Version());

            await Task.Run(() => x.Run(serviceArguments));
            var canaryType = typeof(MetricsConfiguration);

            Console.SetOut(prOut);

            Assert.True(
                Regex.IsMatch(buffer.ToString(), $"(OK|ERROR).*{canaryType.FullName}"),
                "Output should contain a row with validation of the type");
            
            Console.WriteLine(buffer);
        }
    }
}
