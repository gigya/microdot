using System;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Service;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{

    [TestFixture]
    public class MicroServiceTests
    {
        private TestingKernel<TraceLog> _testingKernel;
        private NonOrleansServiceTester<CalculatorServiceHost> _serviceTester;
        private ICalculatorService _calculatorService;
        private ServiceTracingContext _tracingContext;

        [OneTimeSetUp]
        public void SetUp()
        {
            try
            {
                _tracingContext = new ServiceTracingContext();
                _testingKernel = new TestingKernel<TraceLog>(kernel => kernel.Rebind<ITracingContext>().ToConstant(_tracingContext));
                _serviceTester = _testingKernel.GetServiceTesterForNonOrleansService<CalculatorServiceHost>(1111, TimeSpan.FromSeconds(10));
                _calculatorService = _serviceTester.GetServiceProxy<ICalculatorService>();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _serviceTester.Dispose();
        }


        [Test]
        public async Task ShouldCallSelfHostServcie()
        {

            (await _calculatorService.Add(1, 2)).ShouldBe(3);
        }

        [Test]
        public async Task TracingContext_RetreivingTraceProperties_IsEquivalent()
        {
            _tracingContext.RequestID = "ReqId";
            _tracingContext.SetSpan("SpanIdButWillBeParentId", null);

            var actual = await _calculatorService.RetriveTraceContext();


            actual.CallId.ShouldBe(_tracingContext.RequestID);
            actual.ParentID.ShouldBe(_tracingContext.SpanID);

        }

        
        #region Mock Data For TracingContext Tests
        [Serializable]
        public class TracingContextForMicrodotServiceMock
        {
            public string CallId { get; set; }
            public string SpanID { get; set; }
            public string ParentID { get; set; }
        }
        #endregion

    }
}