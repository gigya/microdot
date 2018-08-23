using System;
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
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                serviceTester?.Dispose();
                testingKernel.Dispose();
            }


        }

    }
}