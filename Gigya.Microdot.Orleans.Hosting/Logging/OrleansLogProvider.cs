using System;
using Microsoft.Extensions.Logging;

namespace Gigya.Microdot.Orleans.Hosting.Logging
{
    public class OrleansLogProvider :ILoggerProvider
    {
        private readonly Func<string, OrleansLogAdapter> _logFactory;
        public OrleansLogProvider(Func<string, OrleansLogAdapter> logFactory)
        {
            _logFactory = logFactory;
        }

        public void Dispose()
        {
         //   throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
           return   _logFactory(categoryName);
        }
    }
}