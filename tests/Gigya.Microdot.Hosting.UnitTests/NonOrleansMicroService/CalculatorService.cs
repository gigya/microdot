using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.UnitTests.NonOrleansMicroService
{
    public class CalculatorService : ICalculatorService
    {
        public Task<int> Add(int a, int b)
        {
            return Task.FromResult(a + b);
        }
    }
}