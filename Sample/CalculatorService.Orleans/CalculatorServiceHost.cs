using System;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Ninject.Host;
using System.Threading.Tasks;
using Orleans;

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

        protected override async Task AfterOrleansStartup(IGrainFactory grainFactory)
        {
            //StartGeneratingPerSiloGrainMessages(grainFactory);
        }

        public override Type PerSiloGrainType => typeof(MyPerSiloGrain);
        async void StartGeneratingPerSiloGrainMessages(IGrainFactory grainFactory)
        {
            int count = 0;
            while (true) {
                var grain = grainFactory.GetGrain<IMyNormalGrain<int>>(0);
                try { await grain.Publish(count++); }
                catch {}
                await Task.Delay(2000);
            }
        }
    }
}
