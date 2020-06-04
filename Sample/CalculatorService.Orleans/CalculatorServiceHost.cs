using System;
using System.Net;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.Hosting.Configuration;
using Gigya.Microdot.Interfaces.Configuration;

namespace CalculatorService.Orleans
{

    class CalculatorServiceHost : MicrodotOrleansServiceHost
    {
        protected CalculatorServiceHost(HostConfiguration configuration) : base(configuration)
        {
        }

        public string ServiceName => nameof(CalculatorService);

        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", Environment.CurrentDirectory);
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", "");
            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", Environment.CurrentDirectory);
            Environment.SetEnvironmentVariable("REGION", "us1");
            Environment.SetEnvironmentVariable("ZONE", "us1a");
            Environment.SetEnvironmentVariable("ENV", "dev");
            Environment.SetEnvironmentVariable("Consul", "not-real-url");
            var config = 
                new HostConfiguration(
                    new EnvironmentVarialbesConfigurationSource(),
                    new ApplicationInfoSource(
                        new CurrentApplicationInfo(nameof(CalculatorService), Environment.UserName, Dns.GetHostName())));

            try
            {
                new CalculatorServiceHost(config).Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        public override ILoggingModule GetLoggingModule() => new NLogModule();
    }
}
