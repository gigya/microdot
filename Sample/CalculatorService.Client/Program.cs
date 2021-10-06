using CalculatorService.Interface;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.UnitTests.Caching;
using Ninject;
using System;
using System.Threading.Tasks;

namespace CalculatorService.Client
{
    class Program
    {
        static FakeRevokingManager _fakeRevokingManager = new FakeRevokingManager();

        static async Task Main(string[] args)
        {
            try
            {
                Random rnd = new Random(Guid.NewGuid().GetHashCode());
                using (var microdotInitializer = new MicrodotInitializer("test-client", new NLogModule(), k =>
                    {
                        k.Rebind<IRevokeListener>().ToConstant(_fakeRevokingManager);
                    }))
                {
                    //NLog.LogManager.GlobalThreshold = NLog.LogLevel.Info; 
                    var calculatorService = microdotInitializer.Kernel.Get<ICalculatorService>();

                    Task.Factory.StartNew(() => ListenToRevokes());

                    while (true)
                    {
                        try
                        {
                            int a = rnd.Next(100);
                            int b = rnd.Next(100);
                            var result = await calculatorService.Add(a, b);
                            Console.WriteLine($"{a}+{b}={result}");
                            await Task.Delay(1000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error: {e.Message}");
                            throw;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        private static async Task ListenToRevokes()
        {
            while (true)
            {
                var revokeKey = Console.ReadLine();

                Console.WriteLine($"Before revoke of {revokeKey}");
                await _fakeRevokingManager.Revoke(revokeKey);
                Console.WriteLine($"After revoke of {revokeKey}");
            }
        }
    }
}
