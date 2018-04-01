using System.Threading;

namespace Gigya.Microdot.SharedLogic
{
    public interface IServcieDrainToken
    {
        CancellationToken Token { get; }
    }

    internal interface IServcieDrainSource : IServcieDrainToken
    {
        void StartDrain();
    }

    internal class ServcieDrainSource : IServcieDrainSource
    {
        public CancellationTokenSource Source = new CancellationTokenSource();

        void IServcieDrainSource.StartDrain()
        {
            Source.Cancel();
        }

        CancellationToken IServcieDrainToken.Token => Source.Token;
    }
}