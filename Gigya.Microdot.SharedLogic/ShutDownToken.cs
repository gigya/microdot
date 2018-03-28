using System.Threading;

namespace Gigya.Microdot.SharedLogic
{
    public interface IServcieShutdownToken
    {
        CancellationToken Token { get; }
    }

    internal interface IServcieShutdownSource : IServcieShutdownToken
    {
        void Shutdown();
    }

    internal class ShutdownToken : IServcieShutdownSource
    {
        public CancellationTokenSource Source = new CancellationTokenSource();

        void IServcieShutdownSource.Shutdown()
        {
            Source.Cancel();
        }

        CancellationToken IServcieShutdownToken.Token => Source.Token;
    }
}