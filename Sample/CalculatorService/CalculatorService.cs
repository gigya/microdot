using System.Threading.Tasks;
using CalculatorService.Interface;

namespace CalculatorService
{

    class CalculatorService: ICalculatorService
    {

        public Task<int> Add(int a, int b)
        {
            return Task.FromResult(a + b);
        }
    }
}
