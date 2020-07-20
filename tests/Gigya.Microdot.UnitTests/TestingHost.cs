using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.HttpService;
using Ninject;
using Ninject.Syntax;
using NSubstitute;

namespace Gigya.Microdot.UnitTests
{
    public class TestingHost<T> : MicrodotServiceHost<T> where T : class
    {
        private static string GenerateServiceName()
        {
            // Last word is good enought for randomization, but easier to follow
            return $"TestingHost-{ Guid.NewGuid().ToString().Substring(24) }";
        }

        public T Instance { get; private set; }

        public override string ServiceName => "test";

        protected override ILoggingModule GetLoggingModule() { return new FakesLoggersModules(); }

        protected override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
        {
            var env = new HostEnvironment(new TestHostEnvironmentSource());

            kernel.Rebind<IEnvironment>().ToConstant(env).InSingletonScope();
            kernel.Rebind<CurrentApplicationInfo>().ToConstant(env.ApplicationInfo).InSingletonScope();

            base.PreConfigure(kernel, Arguments);
        }

        protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
        {
            
            kernel.Rebind<ILog>().ToConstant(new ConsoleLog());

            kernel.Rebind<IConfigurationDataWatcher, ManualConfigurationEvents>()
                  .To<ManualConfigurationEvents>()
                  .InSingletonScope();

            kernel.Rebind<IEventPublisher>().To<NullEventPublisher>();
            kernel.Rebind<IWorker>().To<WaitingWorker>();
            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>().InSingletonScope();
            kernel.Rebind<ICertificateLocator>().To<DummyCertificateLocator>().InSingletonScope();

            kernel.Bind<T>().ToConstant(Substitute.For<T>());

            Instance = kernel.Get<T>();
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