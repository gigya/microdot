using System;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Ninject.Host;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Ninject.Host;

namespace CalculatorService.Orleans
{

    class CalculatorServiceHost : MicrodotOrleansServiceHost
    {
        public CalculatorServiceHost(HostEnvironment environment, Version infraVersion) : base(environment, infraVersion)
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
            Environment.SetEnvironmentVariable("CONSUL", "addr");

            var config =
                new HostEnvironment(
                    new EnvironmentVarialbesConfigurationSource(),
                    new ApplicationInfoSource(new Gigya.Microdot.Interfaces.Configuration.CurrentApplicationInfo(nameof(CalculatorService), Environment.UserName, System.Net.Dns.GetHostName())));

            try
            {
                new CalculatorServiceHost(config, new Version()).Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        public override ILoggingModule GetLoggingModule() => new NLogModule();
    }
}
