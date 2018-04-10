using System.Threading;

namespace Gigya.Microdot.SharedLogic
{
    public interface IServiceDrainListener
    {
        CancellationToken Token { get; }
    }

    internal class ServiceDrainController :IServiceDrainListener
    {
        public CancellationTokenSource Source = new CancellationTokenSource();

        internal void StartDrain()
        {
            Source.Cancel();
        }

        CancellationToken IServiceDrainListener.Token => Source.Token;
    }
}