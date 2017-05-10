using System.Threading.Tasks;

using Orleans;
using Orleans.Concurrency;

namespace Gigya.Microdot.Orleans.Hosting
{
    public delegate Task RequestProcessingAction();

    [Unordered]
	public interface IRequestProcessingGrain : IGrainWithIntegerKey
	{
        Task Do(Immutable<RequestProcessingAction> action);
	}

    [ExcludeGrainFromStatistics]
    [StatelessWorker, Reentrant]
	public class RequestProcessingGrain : Grain, IRequestProcessingGrain
	{
        public async Task Do(Immutable<RequestProcessingAction> action)
	    {
	        await action.Value();
	    }
	}
   
    
}
