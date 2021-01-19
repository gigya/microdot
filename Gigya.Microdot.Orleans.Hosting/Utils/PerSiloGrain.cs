using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Orleans.Concurrency;
using Orleans.Core;
using Orleans.Runtime;
using Orleans.Services;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting.Utils
{

    /// <summary>An interface implemented by <see cref="PerSiloGrain<T>"/>. Used to send a message to a specific
    /// service grain on a specific silo.</summary>
    public interface IPerSiloGrain: IGrainService
    {
        Task OnMessage(string message);
    }


    /// <summary>An abstract service grain class that you can derive from. Derived instances will be instantiated once
    /// per Silo at startup and remain active till the silo shuts down. Use MicrodotOrleansServiceHost.PerSiloGrainType
    /// to register your grain type with Orleans.
    /// Use <see cref="PerSiloGrainClient<typeparamref name="T"/>"/> (with the same genereic type <typeparamref name="T"/>)
    /// to send a message to each instance of your derived class (one per silo). It will trigger a call to the
    /// <see cref="OnMessage"/> method which you need to implement. This can be used to implement various patterns such
    /// as multiple-readers, single-writer, i.e. having a per-silo cache to accelerate read operations and invalidating
    /// items in that cache (per silo) when the underlying data is modified.</summary>
    /// <typeparam name="T">A concrete message type for the messages you wish to send to each per-silo instace of that grain.</typeparam>
    [Reentrant]
    public abstract class PerSiloGrain<T>: GrainService, IPerSiloGrain
    {
        protected IGrainFactory GrainFactory;

        public PerSiloGrain(IGrainIdentity id, Silo silo, ILoggerFactory loggerFactory, IGrainFactory grainFactory)
            : base(id, silo, loggerFactory)
        {
            GrainFactory = grainFactory;
        }

        Task IPerSiloGrain.OnMessage(string message) => OnMessage(JsonConvert.DeserializeObject<T>(message));
        protected abstract Task OnMessage(T message);
    }
}
