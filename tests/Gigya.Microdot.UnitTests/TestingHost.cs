using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Ninject;
using Ninject.Syntax;
using NSubstitute;

namespace Gigya.Microdot.UnitTests
{


    public class TestingHost<T> : MicrodotServiceHost<T> where T : class
    {
        public T Instance { get; private set; }

        private readonly Action<IKernel> _configure;
        private readonly Action<IKernel> _onInitialize;

        private IKernel _kernel;

        public TestingHost()
        {
        }

        public TestingHost(Action<IKernel> onInitialize)
        {
            _onInitialize = onInitialize;

        }


        protected override ILoggingModule GetLoggingModule() { return new FakesLoggersModules(); }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            _kernel = kernel;
            kernel.Rebind<ILog>().ToConstant(new ConsoleLog());

            kernel.Rebind<IConfigurationDataWatcher, ManualConfigurationEvents>()
                  .To<ManualConfigurationEvents>()
                  .InSingletonScope();


            kernel.Rebind<IEventPublisher>().To<NullEventPublisher>();
            kernel.Rebind<IWorker>().To<WaitingWorker>();
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>().InSingletonScope();

            kernel.Bind<T>().ToConstant(Substitute.For<T>());

            _configure?.Invoke(kernel);

            Instance = kernel.Get<T>();
        }

        protected override void OnInitilize(IResolutionRoot resolutionRoot)
        {
            base.OnInitilize(resolutionRoot);
            _onInitialize?.Invoke(_kernel);
        }

        private class WaitingWorker : IWorker
        {
            private readonly ConcurrentStack<Task> tasks = new ConcurrentStack<Task>();

            public void FireAndForget(Func<Task> asyncAction)
            {
                var task = asyncAction();
                tasks.Push(task);
            }


            public void Dispose() { Task.WaitAll(tasks.ToArray()); }
        }


        private class FakesLoggersModules : ILoggingModule
        {

      
            public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding, IBindingToSyntax<Func<string, ILog>> logFactory)
            {
        
                    logBinding.To<ConsoleLog>();

                logFactory.ToMethod(c => caller => c.Kernel.Get<ConsoleLog>());
                eventPublisherBinding.To<NullEventPublisher>();
            }
        }
    }
}