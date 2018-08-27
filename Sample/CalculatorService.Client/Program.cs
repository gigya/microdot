using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CalculatorService.Interface;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Logging.NLog;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.Factory;

namespace CalculatorService.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", Environment.CurrentDirectory);
                Environment.SetEnvironmentVariable("GIGYA_CONFIG_PATHS_FILE", "");
                Environment.SetEnvironmentVariable("GIGYA_ENVVARS_FILE", Environment.CurrentDirectory);
                Environment.SetEnvironmentVariable("DC", "global");
                Environment.SetEnvironmentVariable("ENV", "dev");

                CurrentApplicationInfo.Init("CalculatorService.Client");

                var kernel = new StandardKernel();
                kernel.Load<MicrodotModule>();
                kernel.Load<NLogModule>();

                kernel.Get<Func<DiscoveryConfig>>()();
                kernel.Get<Func<DiscoveryConfig>>()();
                DiscoveryConfig dConfig = kernel.Get<DiscoveryConfig>();
                ConfigCreatorTest configCreator = kernel.Get<ConfigCreatorTest>();
                IConfigObject config = configCreator.GetDiscoveryConfig();
                DiscoveryConfig dConfig2 = configCreator.GetDiscConfig();
                bool dConfigsEqual = ReferenceEquals(dConfig, dConfig2);
                ISourceBlock<CacheConfig> sourceBlockByFunc = configCreator.GetISourceBlockByFunc();
                ISourceBlock<MyConfig> sourceBlockDirect = configCreator.GetISourceBlockDirect();
                ISourceBlock<MyConfig> sourceBlockByFactory = configCreator.GetISourceBlockByFactory();
                bool sourceBlocksAreEqual = ReferenceEquals(sourceBlockDirect, sourceBlockByFactory);
                Console.WriteLine("Sleeping");
                Thread.Sleep(5000);
                Console.WriteLine("Starting test...");

                //TestService(serviceCreator, 200000);
                TestConfig(kernel, configCreator, 2000000);

                Console.ReadLine();

                //Console.WriteLine($"Sum: {sum}");
            }
            catch (Exception ex)

            {
                Console.Error.WriteLine(ex);
            }
        }

        private static dynamic GetGenericFuncCompiledLambda(Type configType, ConfigObjectCreatorWrapper cocWrapper, string functionName)
        {
            MethodInfo func = typeof(ConfigObjectCreatorWrapper).GetMethod(functionName).MakeGenericMethod(configType);
            Expression instance = Expression.Constant(cocWrapper);
            Expression callMethod = Expression.Call(instance, func);
            Type delegateType = typeof(Func<>).MakeGenericType(configType);
            Type parentExpressionType = typeof(Func<>).MakeGenericType(delegateType);

            dynamic lambda = Expression.Lambda(parentExpressionType, callMethod).Compile();

            return lambda;
        }

        internal static bool IsConfigObject(Type service)
        {
            return service.IsClass && service.IsAbstract == false && typeof(IConfigObject).IsAssignableFrom(service);
        }

        internal static bool IsSourceBlock(Type service)
        {
            return
                service.IsGenericType &&
                service.GetGenericTypeDefinition() == typeof(ISourceBlock<>) &&
                IsConfigObject(service.GetGenericArguments().Single());
        }

        private static ConfigObjectCreator GetCreator(IKernel kernel, Type type)
        {
            var getCreator = kernel.Get<Func<Type, ConfigObjectCreator>>();
            var uninitializedCreator = getCreator(type);
            uninitializedCreator.Init();
            return uninitializedCreator;
        }

        private static void TestService(ServiceCrreatorTest serviceCreator, int calls)
        {   
            Console.WriteLine("Service test is started");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < calls; i++)
            {
                ICalculatorService calculatorService = serviceCreator.GetCalculatorService();
                //int sum = calculatorService.Add(2, 3).Result;
            }

            sw.Stop();

            Console.WriteLine($"Service test is finished with {calls} calls in {sw.Elapsed.TotalSeconds} seconds");
        }

        private static void TestConfig(StandardKernel kernel, ConfigCreatorTest configCreator, int calls)
        {
            Console.WriteLine("Config test is started");
            ParallelOptions pOptions = new ParallelOptions();
            pOptions.MaxDegreeOfParallelism = 4;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.For(0, calls, pOptions, i =>
                                                {
                                                    CacheConfig config = configCreator.GetConfig();
                                                    //Console.WriteLine(config.LogRevokes);
                                                    //kernel.Get<Func<CacheConfig>>()();
                                                });

            sw.Stop();

            Console.WriteLine($"Config test is finished with {calls} calls in {sw.Elapsed.TotalSeconds} seconds");
        }
    }
}
