using System;
using System.Threading.Tasks;
using CalculatorService.Interface;
using Orleans;
using Orleans.Concurrency;

namespace CalculatorService.Orleans
{

    public interface ICalculatorServiceGrain : ICalculatorService, IGrainWithIntegerKey { }


    [StatelessWorker, Reentrant]
    public class CalculatorService : Grain, ICalculatorServiceGrain
    {

        public CalculatorService()
        {
            //    throw  new Exception();
        }
        Random x = new Random();
        public Task<int> Add(int a, int b)
        {
            int grainId = x.Next(0, 1000);
            var grain = GrainFactory.GetGrain<ICalculatorServiceGrain2>(grainId);
            return grain.Add(a, b);
        }
    }

    public interface ICalculatorServiceGrain2 : IGrainWithIntegerKey
    {
        Task<int> Add(int a, int b);
    }


    public class CalculatorService2 : Grain, ICalculatorServiceGrain2
    {

        public CalculatorService2()
        {
                throw  new Exception();
        }

        public Task<int> Add(int a, int b)
        {
            return Task.FromResult(a + b);
        }
    }

}
