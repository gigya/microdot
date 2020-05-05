using System;
using System.Collections.Generic;
using System.Net.Http;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.Testing.Shared;
using Metrics;

using Ninject;
using Ninject.Parameters;

using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
  
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public abstract class AbstractServiceProxyTest
    {
        protected TestingKernel<ConsoleLog> unitTesting;
        protected Dictionary<string, string> MockConfig { get; } = new Dictionary<string, string>();
        protected JsonExceptionSerializer ExceptionSerializer { get; set; }

        [SetUp]
        public virtual void SetUp()
        {
            
            unitTesting = new TestingKernel<ConsoleLog>(mockConfig: MockConfig);
            Metric.ShutdownContext(ServiceProxyProvider.METRICS_CONTEXT_NAME);
            ExceptionSerializer = unitTesting.Get<JsonExceptionSerializer>();
        }


        [TearDown]
        public virtual void TearDown()
        {
            Metric.ShutdownContext(ServiceProxyProvider.METRICS_CONTEXT_NAME);
        }


        protected IDemoService CreateClient()
        {
            return unitTesting
                .Get<ServiceProxyProviderSpy<IDemoService>>()
                .Client;
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class ServiceProxyProviderSpy<T> : ServiceProxyProvider<T>
    {
        public ServiceProxyProviderSpy(Func<string, IServiceProxyProvider> serviceProxyFactory)
            : base(serviceProxyFactory)
        {
        }
    }
}
