using System;
using System.Threading.Tasks;
using CalculatorService.Interface;
using Gigya.ServiceContract.HttpService;
using Orleans;
using Orleans.Concurrency;

namespace CalculatorService.Orleans
{

    public interface ICalculatorServiceGrain : ICalculatorService, IGrainWithIntegerKey
    {
    }

    [StatelessWorker, Reentrant]
    public class CalculatorService : Grain, ICalculatorServiceGrain
    {
        public Task<int> Add(int a, int b)
        {
            return Task.FromResult(a + b);
        }

        public Task<int> Add_Cached(int a, int b)
        {
            return Task.FromResult(a + b);
        }

        public Task<Revocable<int>> Add_CachedAndRevocable(int a, int b)
        {
            return Task.FromResult(new Revocable<int>
            {
                Value = a + b,
                RevokeKeys = new[] { "a", "b" }
            });
        }
    }
}
