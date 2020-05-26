using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.HttpService;
using Metrics;
using Metrics.MetricData;

using NUnit.Framework;

using RichardSzalay.MockHttp;

// ReSharper disable UnusedVariable


namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
    [TestFixture, Parallelizable(ParallelScope.None)]

    public class MetricsTests : AbstractServiceProxyTest
    {
        [Test]
        public async Task SuccessTest()
        {            
            var resMessage = HttpResponseFactory.GetResponse(content:"''");

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(resMessage);

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            await CreateClient().ToUpper("aaaa");

            var expected = DefaultExpected();

            expected.Counters.Add(new MetricDataEquatable { Name = "Success", Unit = Unit.Calls });
            expected.Timers.Add(new MetricDataEquatable {Name = "Deserialization", Unit = Unit.Calls});

            GetMetricsData().AssertEquals(expected);
        }

        [Test]
        public async Task RequestTimeoutTest()
        {
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(a => { throw new TaskCanceledException(); });

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            try
            {
                await CreateClient().ToUpper("aaaa");
            }
            catch (Exception)
            {
                var expected = DefaultExpected();
                
                expected.Counters.Add(new MetricDataEquatable { Name = "Failed", Unit = Unit.Calls, SubCounters = new[] {"RequestTimeout"} });

                GetMetricsData().AssertEquals(expected);
            }            
        }

        [Test]
        public async Task NotGigyaServerBadRequestTest()
        {            
            var resMessage = HttpResponseFactory.GetResponse(HttpStatusCode.ServiceUnavailable, isGigyaHost:false);

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(resMessage);

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            try
            {
                await CreateClient().ToUpper("aaaa");
            }
            catch (Exception)
            {
                var expected = DefaultExpected();

                expected.Counters.Add(new MetricDataEquatable { Name = "HostFailure", Unit = Unit.Calls });

                GetMetricsData().AssertEquals(expected);
            }
        }

        [Test]
        public async Task NotGigyaServerFailureTest()
        {
            var resMessage = HttpResponseFactory.GetResponse(isGigyaHost: false);

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(resMessage);

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            try
            {
                await CreateClient().ToUpper("aaaa");
            }
            catch (Exception)
            {
                var expected = DefaultExpected();

                expected.Counters.Add(new MetricDataEquatable { Name = "HostFailure", Unit = Unit.Calls });

                GetMetricsData().AssertEquals(expected);
            }
        }

        [Test]
        public async Task JsonSerializationExceptionTest()
        {
            var resMessage = HttpResponseFactory.GetResponse(content: "{");

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(resMessage);

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            try
            {
                await CreateClient().ToUpper("aaaa");
            }
            catch (Exception)
            {
                var expected = DefaultExpected();

                expected.Counters.Add(new MetricDataEquatable { Name = "Failed",  Unit = Unit.Calls  });
                expected.Timers.Add(new MetricDataEquatable { Name = "Deserialization", Unit = Unit.Calls });

                GetMetricsData().AssertEquals(expected);
            }
        }

        [Test]
        public async Task HostsFailureTest()
        {
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(a => { throw new HttpRequestException(); });

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            try
            {
                await CreateClient().ToUpper("aaaa");
            }
            catch (Exception)
            {
                var expected = DefaultExpected();                
                expected.Counters = new List<MetricDataEquatable>()
                {
                    new MetricDataEquatable { Name = "HostFailure", Unit = Unit.Calls }
                };
                
                GetMetricsData().AssertEquals(expected);
            }   
        }

        [Test]
        public async Task NullExceptionReceivedTest()
        {
            var resMessage = HttpResponseFactory.GetResponseWithException(ExceptionSerializer, new NullReferenceException());

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(resMessage);

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            try
            {
                await CreateClient().ToUpper("aaaa");
            }
            catch (Exception)
            {
                var expected = DefaultExpected();

                expected.Counters.Add(new MetricDataEquatable { Name = "ApplicationException", Unit = Unit.Calls });
                expected.Timers.Add(new MetricDataEquatable { Name = "Deserialization", Unit = Unit.Calls });
                
                GetMetricsData().AssertEquals(expected);
            }
        }

        [Test]
        public async Task RequestExceptionReceivedTest()
        {
            var resMessage = HttpResponseFactory.GetResponseWithException(ExceptionSerializer, new RequestException("Post not allowed"), HttpStatusCode.MethodNotAllowed);

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(resMessage);

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            try
            {
                await CreateClient().ToUpper("aaaa");
            }
            catch (RequestException)
            {
                var expected = DefaultExpected();

                expected.Counters.Add(new MetricDataEquatable { Name = "ApplicationException", Unit = Unit.Calls });
                expected.Timers.Add(new MetricDataEquatable { Name = "Deserialization", Unit = Unit.Calls});

                GetMetricsData().AssertEquals(expected);
            }            
        }

        private static MetricsData GetMetricsData()
        {
            return
                Metric.Context(ServiceProxyProvider.METRICS_CONTEXT_NAME)
                    .Context("DemoService")
                    .DataProvider.CurrentMetricsData;
        }

        private static MetricsDataEquatable DefaultExpected()
        {
            return new MetricsDataEquatable
            {
                Counters = new List<MetricDataEquatable>(),
                Timers = new List<MetricDataEquatable> {
                    new MetricDataEquatable{Name = "Serialization",Unit= Unit.Calls},
                    new MetricDataEquatable{Name = "Roundtrip",Unit= Unit.Calls}                    
                }
            };
        }
    }
}