using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using CalculatorService.Interface;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Gigya.Microdot.UnitTests.Caching;
using Ninject;

namespace CalculatorService.Client
{
    class Program
    {
        static FakeRevokingManager _fakeRevokingManager = new FakeRevokingManager();

        static async Task Main(string[] args)
        {
            try
            {
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
                            var result = await calculatorService.Add(1, 2);
                            Console.WriteLine($"Value: {result}");
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
