using Gigya.Microdot.Interfaces.Logging;
using Newtonsoft.Json;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.Services;
using Orleans.Serialization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting.Utils
{


    /// <summary>This interface enables sending a message to all instaces of <see cref="PerSiloGrain{T}"/>
    /// (one per silo).</summary>
    /// <typeparam name="T">The type of the custom message you wish to send. Must match the type of your
    /// <see cref="PerSiloGrain{T}"/>.</typeparam>
    public interface IPerSiloGrainClient<T>
    {
        /// <summary>Sends the provided message to all instaces of <see cref="PerSiloGrain{T}"/> (one per silo), calling
        /// their <see cref="PerSiloGrain{T}.OnMessage(T)"/> method.</summary>
        /// <param name="message">Your custom message data structure.</param>
        /// <exception cref="AggregateException">Thrown when some silos in the cluster are unavailable or didn't respond
        /// within the timeout. It will contain individual exceptions encountered while sending that message to specific
        /// silos.</exception>
        /// <returns>A task that will be completed once all Silos have finished processing the message (or failed to).</returns>
        Task PublishMessageToAllSilos(T message);
    }


    public class PerSiloGrainClient<T> : GrainServiceClient<IPerSiloGrain>
    {
        IGrainReferenceConverter GrainReferenceConverter;
        ISiloStatusOracle SiloStatusOracle;
        ILog Log;


        public PerSiloGrainClient(IServiceProvider serviceProvider, IGrainReferenceConverter grainReferenceConverter,
            ISiloStatusOracle siloStatusOracle, ILog log)
            : base(serviceProvider)
        {
            GrainReferenceConverter = grainReferenceConverter;
            SiloStatusOracle = siloStatusOracle;
            Log = log;
        }


        public async Task PublishMessageToAllSilos(T message)
        {
            var tasks = from siloAddress in SiloStatusOracle.GetApproximateSiloStatuses(onlyActive: true).Keys
                        let remoteGrain = GetGrainServiceForSilo(siloAddress)
                        select CallRemoteSilo(siloAddress, remoteGrain, message);

            await Task.WhenAll(tasks);
        }


        private IPerSiloGrain GetGrainServiceForSilo(SiloAddress address)
        {
            // This slightly misuses a public interface intended for
            // serialization support, but is cleaner than private reflection.
            var keyinfo = ((GrainReference)GrainService).ToKeyInfo();
            var targetKeyInfo = new GrainReferenceKeyInfo(keyinfo.Key, (address.Endpoint, address.Generation));
            var targetRef = GrainReferenceConverter.GetGrainFromKeyInfo(targetKeyInfo);
            return targetRef.Cast<IPerSiloGrain>();
        }


        async Task CallRemoteSilo(SiloAddress siloAddress, IPerSiloGrain remoteGrain, T message)
        {
            try
            {
                await remoteGrain.OnMessage(JsonConvert.SerializeObject(message));
            }
            catch (Exception ex)
            {
                Log.Error(_ => _("Cannot publish message to silo", new { message }, new { siloAddress }, ex));
                throw;
            }
        }
    }
}
