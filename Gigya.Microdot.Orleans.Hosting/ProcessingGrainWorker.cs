using System;
using System.Threading.Tasks;

using Gigya.Microdot.Hosting.HttpService;

using Orleans;
using Orleans.Concurrency;

namespace Gigya.Microdot.Orleans.Hosting
{
    public sealed class ProcessingGrainWorker : IWorker
    {
        private readonly Lazy<IGrainFactory> _grainFactory;

        public ProcessingGrainWorker(Lazy<IGrainFactory> grainFactory)
        {
            _grainFactory = grainFactory;
        }

        public void FireAndForget(Func<Task> asyncAction)
        {
            var infraLongProcessingGrain = _grainFactory.Value.GetGrain<IRequestProcessingGrain>(0);
            infraLongProcessingGrain.Do(new Immutable<RequestProcessingAction>(() => asyncAction())).Ignore();
        }


        public void Dispose() {}
    }
}
