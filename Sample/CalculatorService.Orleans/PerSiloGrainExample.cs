using Gigya.Microdot.Orleans.Hosting.Utils;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Core;
using Orleans.Runtime;
using System;
using System.Threading.Tasks;

namespace CalculatorService.Orleans
{

    public interface IMyNormalGrain<T> : IGrainWithIntegerKey
    {
        Task Publish(T message);
    }


    [Reentrant]
    [StatelessWorker(1)]
    public class MyNormalGrain<T>: Grain, IMyNormalGrain<T>
    {

        PerSiloGrainClient<T> ServiceClient;
        public MyNormalGrain(PerSiloGrainClient<T> serviceClient)
        {
            ServiceClient = serviceClient;
        }

        public async Task Publish(T message)
        {
            await ServiceClient.PublishMessageToAllSilos(message);
        }
    }


    public class MyPerSiloGrain : PerSiloGrain<int>
    {
        public MyPerSiloGrain(IGrainIdentity id, Silo silo, ILoggerFactory loggerFactory, IGrainFactory grainFactory)
            : base(id, silo, loggerFactory, grainFactory)
        {}

        protected override async Task OnMessage(int message)
        {
            Console.WriteLine("Got: " + message);
        }
    }

}
