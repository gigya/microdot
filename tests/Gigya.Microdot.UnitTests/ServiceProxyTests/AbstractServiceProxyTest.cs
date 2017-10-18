﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.Testing;

using Metrics;

using Ninject;
using Ninject.Parameters;

using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
  
    [TestFixture]
    public abstract class AbstractServiceProxyTest
    {     
        internal const string SERVICE_NAME = "Demonstration";
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


        protected IDemoService CreateClient(HttpMessageHandler mockHttpMessageHandler = null)
        {
            return unitTesting
                .Get<ServiceProxyProviderSpy<IDemoService>>(new ConstructorArgument("httpMessageHandler", mockHttpMessageHandler ?? new WebRequestHandler()))
                .Client;
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
