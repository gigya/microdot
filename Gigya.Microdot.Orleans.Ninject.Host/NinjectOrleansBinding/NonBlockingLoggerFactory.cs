using Gigya.Microdot.Orleans.Hosting.Logging;
using Microsoft.Extensions.Logging;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Replacing the original Microsoft Logger factory to avoid blocking code.
    /// Ninject using lock by scope which leading to deadlock in this scenario.
    /// </summary>
    public class NonBlockingLoggerFactory : ILoggerFactory
    {
        // ReSharper disable once InconsistentNaming
        private ILoggerProvider LoggerProvider;
        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return LoggerProvider.CreateLogger(categoryName);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            LoggerProvider = provider;
        }

        public NonBlockingLoggerFactory(OrleansLogProvider provider)
        {
            LoggerProvider = provider;
        }
    }
}