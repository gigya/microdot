using System.Threading.Tasks;
using CalculatorService.Interface;
using Gigya.ServiceContract.HttpService;

namespace CalculatorService
{

    class CalculatorService: ICalculatorService
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
                RevokeKeys = new []{"a", "b" }
            });
        }
    }
}
