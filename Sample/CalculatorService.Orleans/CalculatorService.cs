using System.Threading.Tasks;
using CalculatorService.Interface;
using Orleans;
using Orleans.Concurrency;

namespace CalculatorService.Orleans
{

    public interface ICalculatorServiceGrain : ICalculatorService, IGrainWithIntegerKey { }


    [StatelessWorker,Reentrant]
    public class CalculatorService: Grain, ICalculatorServiceGrain
    {

        public Task<int> Add(int a, int b)
        {
            return Task.FromResult(a + b);
        }
    }
}
