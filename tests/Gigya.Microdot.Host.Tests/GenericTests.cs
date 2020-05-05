using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Host.Tests.Utils;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.Configuration;
using Gigya.Microdot.Hosting.Metrics;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Gigya.Microdot.LanguageExtensions;

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

            var serviceArguments = new ServiceArguments(ServiceStartupMode.VerifyConfigurations, ConsoleOutputMode.Standard, SiloClusterMode.PrimaryNode, 8555);

            var config = new HostConfiguration(
                new TestHostConfigurationSource(
                    region: "us1",
                    zone: "zone",
                    deploymentEnvironment: "_Test",
                    loadPathsFile: Path.Combine(Directory.GetCurrentDirectory(), "loadPaths.json").To(p => new FileInfo(p))));

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

        [Fact]
        public async Task HostShouldInvokeEventsInCorrectOrder()
        {
            string result = "";
            
            var config =
                new HostConfiguration(
                    new TestHostConfigurationSource(
                        loadPathsFile: Path.Combine(Directory.GetCurrentDirectory(), "loadPaths.json").To(p => new FileInfo(p))));

            var host = new Ninject.Host.Host(
                config,
                new CalculatorKernelConfig(),
                new Version());

            host.OnStarting += (s, e) => result += "1";
            host.OnStarted  += (s, e) => result += "2";
            host.OnStopping += (s, e) => result += "3";
            host.OnStopped  += (s, e) => result += "4";

            // TODO: create a correct async model for host tasks
            var tcs = new TaskCompletionSource<bool>();
            host.OnStarted += (s, e) => tcs.SetResult(true);
            
            var hostTask = Task.Run(() => host.Run());
            await tcs.Task;

            await Task.Delay(1000);

            tcs = new TaskCompletionSource<bool>();
            host.OnStopped += (s, e) => tcs.SetResult(true);

            host.Stop();
            await tcs.Task;

            Assert.Equal("1234", result);
        }

        [Theory(Skip = "Enable when host has async model."), Repeat(5)]
        public void HostShouldStartAndStopMultipleTimes(int count)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Console.WriteLine($"-----------------------------Start run {count} time---------------");
            try
            {
                //var host = new ServiceTester<TestHost>(new HostConfiguration(new TestHostConfigurationSource()));

                var host = new Ninject.Host.Host(
                    new HostConfiguration(new TestHostConfigurationSource()),
                    new TestOrleansKernelConfigurator(),
                    new Version()
                    );

                host.Run();

                Console.WriteLine($"-----------------------------Silo Is running {count} time took, {sw.ElapsedMilliseconds}ms---------------");
                host.Dispose();
            }
            finally
            {
                Console.WriteLine(
                    $"-----------------------------End run {count} time, took {sw.ElapsedMilliseconds}ms  ---------------");
            }
        }

        class TestOrleansKernelConfigurator : OrleansKernelConfigurator
        {
            public override ILoggingModule GetLoggingModule()
            {
                return new FakesLoggersModules();
            }

            public override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
            {
                base.PreConfigure(kernel, Arguments);

                Console.WriteLine($"-----------------------------Silo is RebindForTests");
                kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();
                kernel.RebindForTests();
            }

            public class MockServiceValidator : ServiceValidator
            {
                public MockServiceValidator()
                    : base(new List<IValidator>().ToArray())
                {

                }
            }
        }
    }
}
