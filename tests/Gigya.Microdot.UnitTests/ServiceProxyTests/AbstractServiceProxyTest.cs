using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.Testing.Shared;
using Metrics;

using Ninject;
using NUnit.Framework;
using RichardSzalay.MockHttp;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
  
    [TestFixture]
    public abstract class AbstractServiceProxyTest
    {
        protected TestingKernel<ConsoleLog> unitTesting;
        protected Dictionary<string, string> MockConfig { get; } = new Dictionary<string, string>();
        protected JsonExceptionSerializer ExceptionSerializer { get; set; }

        [SetUp]
        public virtual void SetUp()
        {
            TracingContext.SetUpStorage();
            
            unitTesting = new TestingKernel<ConsoleLog>(mockConfig: MockConfig);
            Metric.ShutdownContext(ServiceProxyProvider.METRICS_CONTEXT_NAME);
            TracingContext.SetRequestID("1");
            ExceptionSerializer = unitTesting.Get<JsonExceptionSerializer>();
        }


        [TearDown]
        public virtual void TearDown()
        {
            //clear TracingContext for testing only
            CallContext.FreeNamedDataSlot("#ORL_RC");
            Metric.ShutdownContext(ServiceProxyProvider.METRICS_CONTEXT_NAME);
        }


        protected IDemoService CreateClient(MockHttpMessageHandler handler = null)
        {
            if (handler == null)
                unitTesting.Rebind<HttpClient>().ToSelf();
            else
            {
                handler.When("/schema").Respond(HttpResponseFactory.GetResponse(content: "{ 'Interfaces': [] }"));
                unitTesting.Rebind<HttpClient>().ToConstant(new HttpClient(handler));
            }

            return unitTesting.Get<IDemoService>();
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class ServiceProxyProviderSpy<T> : ServiceProxyProvider<T>
    {
        public ServiceProxyProviderSpy(Func<string, IServiceProxyProvider> serviceProxyFactory, HttpMessageHandler httpMessageHandler)
            : base(serviceProxyFactory)
        {
            ((ServiceProxyProvider)InnerProvider).HttpMessageHandler = httpMessageHandler;
        }
    }
}
