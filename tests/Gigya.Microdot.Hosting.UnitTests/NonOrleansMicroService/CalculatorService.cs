using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    public class CalculatorService : ICalculatorService
    {
        private readonly ITracingContext _tracingContext;

        public CalculatorService(ITracingContext tracingContext)
        {
            _tracingContext = tracingContext;
        }
        public Task<int> Add(int a, int b)
        {
            return Task.FromResult(a + b);
        }

        public async Task<MicroServiceTests.TracingContextForMicrodotServiceMock> RetriveTraceContext()
        {
            return new MicroServiceTests.TracingContextForMicrodotServiceMock
            {
                CallId = _tracingContext.RequestID,
                SpanID = _tracingContext.SpanID,
                ParentID = _tracingContext.ParentSpnaID
            };
        }

    }
}