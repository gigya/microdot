using System;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Service;
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
            var x = new TestingKernel<TraceLog>();
            var y = x.GetServiceTesterForNonOrleansService<CalculatorServiceHost>(1111,TimeSpan.FromSeconds(10));
            (await y.GetServiceProxy<ICalculatorService>().Add(1, 2)).ShouldBe(3);
        }
    }
}