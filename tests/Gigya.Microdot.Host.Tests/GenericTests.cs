using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Host.Tests.Utils;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Hosting.Metrics;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Ninject;
using System;
using System.Diagnostics;
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

        //[Theory, Repeat(5)]
        //public void HostShouldStartAndStopMultipleTimes(int count)
        //{
        //    count++;
        //    Stopwatch sw = Stopwatch.StartNew();
        //    Console.WriteLine($"-----------------------------Start run {count} time---------------");
        //    try
        //    {
        //        //var host = new ServiceTester<TestHost>(new HostConfiguration(new TestHostConfigurationSource()));

        //        var host = new Ninject.Host.Host(
        //            new HostConfiguration(new TestHostConfigurationSource()),
        //            )

        //        host.GetServiceProxy<ICalculatorService>();
        //        Console.WriteLine($"-----------------------------Silo Is running {count} time took, {sw.ElapsedMilliseconds}ms---------------");
        //        host.Dispose();
        //    }
        //    finally
        //    {
        //        Console.WriteLine(
        //            $"-----------------------------End run {count} time, took {sw.ElapsedMilliseconds}ms  ---------------");
        //    }
        //}

        //class TestOrleansKernelConfigurator : OrleansKernelConfigurator
        //{
        //    public override ILoggingModule GetLoggingModule()
        //    {
        //        return new FakesLoggersModules();
        //    }

        //    public override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        //    {
        //        base.PreConfigure(kernel, Arguments);

        //        Console.WriteLine($"-----------------------------Silo is RebindForTests");
        //        kernel.Rebind<ServiceValidator>().To<CalculatorServiceHost.MockServiceValidator>().InSingletonScope();
        //        kernel.RebindForTests();
        //    }
        //}

        //internal class TestHost : MicrodotOrleansServiceHost
        //{
        //    public TestHost() : base(new HostConfiguration(new TestHostConfigurationSource()))
        //    {
        //    }

        //    public override string ServiceName => "TestService";

        //    public override ILoggingModule GetLoggingModule()
        //    {
        //        return new FakesLoggersModules();
        //    }


        //    protected override void PreConfigure(IKernel kernel)
        //    {
        //        base.PreConfigure(kernel);
        //        Console.WriteLine($"-----------------------------Silo is RebindForTests");
        //        kernel.Rebind<ServiceValidator>().To<CalculatorServiceHost.MockServiceValidator>().InSingletonScope();
        //        kernel.RebindForTests();
        //    }
        //}
    }
}
