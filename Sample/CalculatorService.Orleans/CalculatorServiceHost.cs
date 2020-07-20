using System;
using System.Net;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Ninject.Host;

using Gigya.Microdot.Interfaces.Configuration;

namespace CalculatorService.Orleans
{

    class CalculatorServiceHost : MicrodotOrleansServiceHost
    {
        public override string ServiceName => nameof(CalculatorService);

        static void Main(string[] args)
        {
            try
            {
                new CalculatorServiceHost().Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        public override ILoggingModule GetLoggingModule() => new NLogModule();
    }
}
