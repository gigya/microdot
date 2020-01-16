using System;
using CalculatorService.Interface;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Ninject;

namespace CalculatorService
{

    class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        protected CalculatorServiceHost(HostConfiguration configuration) : base(configuration)
        {
        }

        public override string ServiceName => nameof(ICalculatorService).Substring(1);

        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", Environment.CurrentDirectory);
            Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", "");
            Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", Environment.CurrentDirectory);
            Environment.SetEnvironmentVariable("REGION", "us1");
            Environment.SetEnvironmentVariable("ZONE", "us1a");
            Environment.SetEnvironmentVariable("ENV", "dev");

            var config =
                new HostConfiguration(
                    new EnvironmentVarialbesConfigurationSource());

            try
            {
                new CalculatorServiceHost(config).Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        protected override ILoggingModule GetLoggingModule() => new NLogModule();

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Bind<ICalculatorService>().To<CalculatorService>();
        }
    }
}
