using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService
{
    public interface ICalculatorServiceGrain : ICalculatorService, IGrainWithIntegerKey { }
}