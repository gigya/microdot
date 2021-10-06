using CalculatorService.Interface;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;
using System;

namespace CalculatorService
{

    class CalculatorServiceHost : MicrodotServiceHost<ICalculatorService>
    {
        public override string ServiceName => "CalculatorService";

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

        protected override ILoggingModule GetLoggingModule() => new NLogModule();


        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            kernel.Bind<ICalculatorService>().To<CalculatorService>();
        }

    }
}
