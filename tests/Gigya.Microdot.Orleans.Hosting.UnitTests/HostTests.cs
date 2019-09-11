using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.Testing.Service;
using Ninject;
using NUnit.Framework;
using System;
using System.Diagnostics;
using Gigya.Microdot.Hosting.Validators;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    internal class HostTests
    {
        private static int _counter = 0;

        [Test, Repeat(5)]
        public void HostShouldStartAndStopMultipleTimes()
        {
            _counter++;
            Stopwatch sw = Stopwatch.StartNew();
            Console.WriteLine($"-----------------------------Start run {_counter} time---------------");
            try
            {
                var host = new ServiceTester<TestHost>();
                host.GetServiceProxy<ICalculatorService>();
                Console.WriteLine($"-----------------------------Silo Is running {_counter} time took, {sw.ElapsedMilliseconds}ms---------------");
                 host.Dispose();
            }
            finally
            {
                Console.WriteLine(
                    $"-----------------------------End run {_counter} time, took {sw.ElapsedMilliseconds}ms  ---------------");
            }
        }
    }

    internal class TestHost : MicrodotOrleansServiceHost
    {
        public override string ServiceName => "TestService";

        public override ILoggingModule GetLoggingModule()
        {
            return new FakesLoggersModules();
        }


        protected override void PreConfigure(IKernel kernel)
        {
            base.PreConfigure(kernel);
            Console.WriteLine($"-----------------------------Silo is RebindForTests");
            kernel.Rebind<ServiceValidator>().To<CalculatorServiceHost.MockServiceValidator>().InSingletonScope();
            kernel.RebindForTests();
          
        }
    }
}