using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.Testing.Service;
using Ninject;
using NUnit.Framework;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class OrleansHostHooksTests
    {
        private class TestHost : MicrodotOrleansServiceHost
        {
            public bool AfterOrleansCalled = false;
            public override string ServiceName => "test";

            public override ILoggingModule GetLoggingModule()
            {
                return new FakesLoggersModules();
            }

            protected override Task AfterOrleansStartup(IGrainFactory grainFactory)
            {
                this.AfterOrleansCalled = true;
                return base.AfterOrleansStartup(grainFactory);
            }

            protected override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
            {
                var env = new HostEnvironment(new TestHostEnvironmentSource());
                kernel.Rebind<IEnvironment>().ToConstant(env).InSingletonScope();
                kernel.Rebind<CurrentApplicationInfo>().ToConstant(env.ApplicationInfo).InSingletonScope();

                base.PreConfigure(kernel, Arguments);
                Console.WriteLine($"-----------------------------Silo is RebindForTests");
                kernel.Rebind<ServiceValidator>().To<CalculatorServiceHost.MockServiceValidator>().InSingletonScope();
                kernel.RebindForTests();
            }
        }

        [Test]
        public void AfterOrleansStartup_ShouldBeCalled()
        {
            var tester = new ServiceTester<TestHost>();

            Assert.IsTrue(tester.Host.AfterOrleansCalled, "AfterOrleansStartup hasn't been called.");

            tester.Dispose();
        }
    }
}
