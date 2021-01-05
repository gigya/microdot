using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using FluentAssertions;

using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Service;
using Newtonsoft.Json;
using Ninject;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.Exceptions;
using NUnit.Framework;

using RichardSzalay.MockHttp;

using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{

    public class BehaviorTests : AbstractServiceProxyTest
    {
        [OneTimeSetUp]
        public void startClean()
        {
            TracingContext.ClearContext();
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            unitTesting.Rebind<ICertificateLocator>().To<DummyCertificateLocator>().InSingletonScope();
        }

        [Test]
        public async Task AllRequestsForSameCallID_SameHostSelected()
        {
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string> {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "host1,host2"},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()}
            };

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req => HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}'"));
                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                        k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                    },
                    dict))
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;

                //If we set Request Id we would like always to select same Host
                TracingContext.SetRequestID("dumyId1");
                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());
                var hostOfFirstReq = (string)await serviceProxy.Invoke(request, typeof(string));
                string host;
                for (int i = 0; i < 50; i++)
                {
                    host = (string)await serviceProxy.Invoke(request, typeof(string));
                    host.ShouldBe(hostOfFirstReq);
                }

                TracingContext.SetRequestID("dumyId2");
                host = (string)await serviceProxy.Invoke(request, typeof(string));
                host.ShouldNotBe(hostOfFirstReq);
            }
        }

        [Test]
        public async Task ServiceProxyRpcMessageShouldRemainSame()
        {
            const string serviceName = "DemoService";
             int defaultPort = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {$"Discovery.Services.{serviceName}.Source", "Config"},
                {$"Discovery.Services.{serviceName}.Hosts", "host1"},
                {$"Discovery.Services.{serviceName}.DefaultPort", defaultPort.ToString()}
            };

            Uri uri = null;
            string requestMessage = null;

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*").Respond(async req =>
                    {
                        requestMessage = await req.Content.ReadAsStringAsync();
                        uri = req.RequestUri;
                        return HttpResponseFactory.GetResponse(HttpStatusCode.Accepted);
                    });
                return messageHandler;
            };

            using (var kernel = new TestingKernel<ConsoleLog>(k =>
            {
                
                k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
            }, dict))
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();

                TracingContext.SetRequestID("g"); 
                
                var serviceProxy = providerFactory(serviceName);

                string expectedHost = "override-host";
                int expectedPort = DisposablePort.GetPort().Port;

                TracingContext.SetHostOverride(serviceName, expectedHost, expectedPort);

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());
                using (TracingContext.Tags.SetUnencryptedTag("test", 1))
                    using (TracingContext.SuppressCaching(CacheSuppress.RecursiveAllDownstreamServices))
                        await serviceProxy.Invoke(request, typeof(string));

                var body = requestMessage;
                Console.WriteLine($"error: {body}");
             
                JsonConvert.DeserializeObject<GigyaRequestProtocol>(body, new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });
                
                uri.Host.ShouldBe(expectedHost);
                uri.Port.ShouldBe(expectedPort);
            }
        }

        public class Arguments
        {
        }

        // Don't change structure, unless the original class is changing on purpose.
        // It used to ensure, the public protocol isn't broken or changed by mistake.
        public class TracingData
        {
            [JsonRequired]
            public string RequestID { get; set; }
           
            [JsonRequired]
            public string HostName { get; set; }
            
            [JsonRequired]
            public string ServiceName { get; set; }
          
            [JsonRequired]
            public string SpanID { get; set; }
            
            [JsonRequired]
            public DateTime SpanStartTime { get; set; }

            [JsonRequired]
            public Dictionary<string, ContextTag> Tags { get; set; }
        }

        public class Host1
        {
            [JsonRequired]
            public string ServiceName { get; set; }
           
            [JsonRequired]
            public string Host { get; set; }
           
            [JsonRequired]
            public int Port { get; set; }
        }

        public class Overrides
        {
            [JsonRequired]
            public List<Host1> Hosts { get; set; }
           
            [JsonRequired]
            public string PreferredEnvironment { get; set; }

            [JsonProperty]
            public CacheSuppress? SuppressCaching { get; set; }
        }

        public class Target
        {
            [JsonRequired]
            public string MethodName { get; set; }
        }

        public class GigyaRequestProtocol
        {
            [JsonRequired]
            public Arguments Arguments { get; set; }
           
            [JsonRequired]
            public TracingData TracingData { get; set; }
           
            [JsonRequired]
            public Overrides Overrides { get; set; }
           
            [JsonRequired]
            public Target Target { get; set; }
        }


        [Test]
        public async Task RequestContextShouldOverrideHostOnly()
        {
            const string serviceName = "DemoService";
            int defaultPort = DisposablePort.GetPort().Port;

            var dict = new Dictionary<string, string> {
                {$"Discovery.Services.{serviceName}.Source", "Config"},
                {$"Discovery.Services.{serviceName}.Hosts", "host1"},
                {$"Discovery.Services.{serviceName}.DefaultPort", defaultPort.ToString()}
            };

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {
                        if (req.Method == HttpMethod.Get && req.RequestUri.Scheme == "https")
                            throw new HttpRequestException();

                        return HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}:{req.RequestUri.Port}'");
                    });
                return messageHandler;
            };

            var kernel = new TestingKernel<ConsoleLog>(k =>
            {
                k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
            }, dict);
            var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory(serviceName);

            string overrideHost = "override-host";


            TracingContext.SetHostOverride(serviceName, overrideHost);

            var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());
            for (int i = 0; i < 50; i++)
            {
                var host = (string)await serviceProxy.Invoke(request, typeof(string));
                host.ShouldBe($"{overrideHost}:{defaultPort}");
            }

        }


        [Test]
        public async Task AllHostsAreHavingNetworkErrorsShouldTryEachOnce()
        {
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string> {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "host1,host2"},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()}
            };

            int counter = 0;
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {
                        bool disableReachabilityChecker = req.Content == null;
                        if (disableReachabilityChecker) throw new HttpRequestException();

                        counter++;
                        throw new HttpRequestException();
                    });

                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                        k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                    }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                Func<Task> act = () => serviceProxy.Invoke(request, typeof(string));
                await act.ShouldThrowAsync<ServiceUnreachableException>();
                counter.ShouldBe(2);
            }
        }


        [Test]
        public async Task OneHostHasNetworkErrorShouldMoveToNextHost()
        {
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "host1,host2"},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()}
            };

            int counter = 0;
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {

                        if (req.Method == HttpMethod.Get && req.RequestUri.Scheme == "https")
                            throw new HttpRequestException();

                        counter++;

                        if (req.RequestUri.Host == "host1") throw new HttpRequestException();
                        return HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}'");
                    });
                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                 k =>
                 {
                     k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                     k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                 }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;
                TracingContext.SetRequestID("1");

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                for (int i = 0; i < 3; i++)
                {
                    var server = await serviceProxy.Invoke(request, typeof(string));
                    server.ShouldBe("host2");
                }

                counter.ShouldBe(3);
            }
        }


        [Test]
        public async Task RequestContextOverrideShouldFailOnFirstAttempt()
        {
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "notImpotent"},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()}
            };

            int counter = 0;
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {
                        if (req.Method == HttpMethod.Get && req.RequestUri.Scheme == "https")
                            throw new HttpRequestException();

                        counter++;

                        throw new HttpRequestException();
                    });
                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                        k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                    }, dict)
            )
            {
                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;

                //Disable  TracingContext.SetRequestID("1");

                CallContext.FreeNamedDataSlot("#ORL_RC");

                string overrideHost = "override-host";
                int overridePort = 5318;
                TracingContext.SetHostOverride("DemoService", overrideHost, overridePort);

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                for (int i = 0; i < 3; i++)
                {
                    Func<Task> act = () => serviceProxy.Invoke(request, typeof(string));

                    await act.ShouldThrowAsync<HttpRequestException>();
                }
                counter.ShouldBe(3);
            }
        }


        [Test]
        public async Task FailedHostShouldBeRemovedFromHostList()
        {
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "local"},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()}
            };

            int counter = 0;
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {
                        bool disableReachabilityChecker = req.Content == null;
                        if (disableReachabilityChecker) throw new HttpRequestException();
                        counter++;

                        throw new HttpRequestException();
                    });
                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                        k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                    }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;

                //Disable  TracingContext.SetRequestID("1");

                CallContext.FreeNamedDataSlot("#ORL_RC");

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                for (int i = 0; i < 10; i++)
                {
                    Func<Task> act = () => serviceProxy.Invoke(request, typeof(string));

                    await act.ShouldThrowAsync<ServiceUnreachableException>();
                }
                counter.ShouldBe(1);
            }
        }




        [Test]
        public async Task ToUpper_MethodCallSucceeds_ResultIsCorrect()
        {
            var expected = "AAAA";
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(HttpResponseFactory.GetResponse(content: $"'{expected}'"));

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            var actual = await CreateClient().ToUpper("aaaa");

            actual.ShouldBe(expected);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithRequestException_CorrectExceptionIsThrown()
        {
            var expected = new RequestException("You request is invalid.").ThrowAndCatch();
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(unitTesting.Get<JsonExceptionSerializer>(), expected));

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            Func<Task> action = async () => await CreateClient().ToUpper("aaaa");

            action.ShouldThrow<RequestException>().Message.Should().Be(expected.Message);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithCustomerFacingException_CorrectExceptionIsThrown()
        {
            var expected = new RequestException("You action is invalid, Mr. Customer.", 30000).ThrowAndCatch();
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(req =>
                {
                    return HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected);
                });
                
                return messageHandler;
            };
            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
            var actual = CreateClient().ToUpper("aaaa").ShouldThrow<RequestException>();

            actual.Message.ShouldBe(expected.Message);
            actual.ErrorCode.ShouldBe(expected.ErrorCode);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithEnvironmentException_CorrectExceptionIsThrown()
        {
            var expected = new EnvironmentException("You environment is invalid.").ThrowAndCatch();
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected));

                return messageHandler;
            };
            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            var actual = CreateClient().ToUpper("aaaa").ShouldThrow<EnvironmentException>();

            actual.Message.ShouldBe(expected.Message);
        }




        [Test]
        public async Task ToUpper_MethodCallFailsWithRemoteServiceException_CorrectExceptionIsThrown()
        {
            var expected = new RemoteServiceException("A service is invalid.", "someUri").ThrowAndCatch();
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            { 
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected));

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            var actual = CreateClient().ToUpper("aaaa").ShouldThrow<RemoteServiceException>();

            actual.Message.ShouldBe(expected.Message);
            actual.RequestedUri.ShouldBe(expected.RequestedUri);
            actual.InnerException.ShouldBeNull();
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithProgrammaticException_CorrectExceptionIsThrown()
        {
            var expected = new ProgrammaticException("You code is invalid.").ThrowAndCatch();
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected));

                return messageHandler;
            };
            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            var actual = CreateClient().ToUpper("aaaa").ShouldThrow<RemoteServiceException>();

            actual.InnerException.ShouldBeOfType<ProgrammaticException>();
            actual.InnerException.Message.ShouldBe(expected.Message);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithInvalidJson_CorrectExceptionIsThrown()
        {
            string badJson = "not JSON!";
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler.When("*").Respond(HttpResponseFactory.GetResponse(HttpStatusCode.InternalServerError, content: badJson));

                return messageHandler;
            };

            unitTesting.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

            var actual = CreateClient().ToUpper("aaaa").ShouldThrow<RemoteServiceException>();

            actual.EncryptedTags["responseContent"].ShouldBe(badJson);
            actual.InnerException.ShouldBeAssignableTo<JsonException>();
        }

        [Test]
        public async Task HttpsNotListening_ContinueWithHttp()
        {
            var host = "host1";
            var httpsPortOffset = 5;
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", host},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()},
                {"Discovery.Services.DemoService.TryHttps", "true"}
            };

            int httpsTestCount = 0;

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("https://*")
                    .Respond(req =>
                    {
                        if (req.RequestUri.AbsoluteUri ==
                            $"https://{host}:{port + httpsPortOffset}/")
                            httpsTestCount++;

                        throw new HttpRequestException();
                    });
                messageHandler
                    .When("http://*")
                    .Respond(req =>
                    {
                        if (req.RequestUri.AbsoluteUri == $"http://{host}:{port}/DemoService.testMethod")
                            return HttpResponseFactory.GetResponse(content: "'someResponse'");
                        throw new HttpRequestException("Invalid uri");
                    });

                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                        k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                        var getConfig = k.Get<Func<DiscoveryConfig>>();
                        k.Rebind<Func<DiscoveryConfig>>().ToMethod(c =>
                        {
                            var config = getConfig();
                            return () => config;
                        });
                    }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;
                TracingContext.SetRequestID("1");

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                for (int i = 0; i < 10; i++)
                {
                    var server = await serviceProxy.Invoke(request, typeof(string));

                    server.ShouldBe("someResponse");
                }

                Assert.That(() => httpsTestCount, Is.EqualTo(1).After(10).Seconds.PollEvery(1).Seconds);
            }
        }


        [Test]
        public async Task HttpsListening_CallHttpsAfterFirstHttpCall()
        {
            var host = "host1";
            var httpsPortOffset = 5;
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", host},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()},
                {"Discovery.Services.DemoService.TryHttps", "true"}
            };

            int httpsTestCount = 0;

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("https://*")
                    .Respond(req =>
                    {
                        if (req.RequestUri.AbsoluteUri == $"https://{host}:{port + httpsPortOffset}/")
                        {
                            httpsTestCount++;
                            return HttpResponseFactory.GetResponse(content: "'some HTTPS response'");
                        }
                        if (req.RequestUri.AbsoluteUri == $"https://{host}:{port + httpsPortOffset}/DemoService.testMethod")
                            return HttpResponseFactory.GetResponse(content: "'some HTTPS response'");
                        throw new HttpRequestException("Invalid uri");
                    });
                messageHandler
                    .When("http://*")
                    .Respond(req =>
                    {
                        if (req.RequestUri.AbsoluteUri == $"http://{host}:{port}/DemoService.testMethod")
                            return HttpResponseFactory.GetResponse(content: "'some HTTP response'");
                        throw new HttpRequestException("Invalid uri");
                    });

                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                 k =>
                 {
                     k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                     k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                     var getConfig = k.Get<Func<DiscoveryConfig>>();
                     k.Rebind<Func<DiscoveryConfig>>().ToMethod(c =>
                     {
                         var config = getConfig();
                         return () => config;
                     });
                 }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;
                TracingContext.SetRequestID("1");

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                for (int i = 0; i < 10; i++)
                {
                    bool httpsTestFinished = httpsTestCount > 0;

                    var server = await serviceProxy.Invoke(request, typeof(string));

                    server.ShouldBe( httpsTestFinished ? "some HTTPS response" : "some HTTP response");
                }

                Assert.That(() => httpsTestCount, Is.EqualTo(1).After(10).Seconds.PollEvery(1).Seconds);
            }
        }

        [Test]
        public async Task HttpsStoppedListening_FallbackToHttp()
        {
            var host = "host1";
            var httpsPortOffset = 5;
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", host},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()},
                {"Discovery.Services.DemoService.TryHttps", "true"}
            };

            int httpsTestCount = 0;
            bool httpsMethodCalled = false;

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("https://*")
                    .Respond(req =>
                    {
                        if (httpsMethodCalled)
                            throw new HttpRequestException("", new WebException("", WebExceptionStatus.ProtocolError));
                        if (req.RequestUri.AbsoluteUri == $"https://{host}:{port + httpsPortOffset}/")
                        {
                            httpsTestCount++;
                            return HttpResponseFactory.GetResponse(content: "'some HTTPS response'");
                        }

                        if (req.RequestUri.AbsoluteUri == $"https://{host}:{port + httpsPortOffset}/DemoService.testMethod")
                        {
                            httpsMethodCalled = true;
                            return HttpResponseFactory.GetResponse(content: "'some HTTPS response'");
                        }

                        throw new HttpRequestException("Invalid uri");
                    });
                messageHandler
                    .When("http://*")
                    .Respond(req =>
                    {
                        if (req.RequestUri.AbsoluteUri == $"http://{host}:{port}/DemoService.testMethod")
                            return HttpResponseFactory.GetResponse(content: "'some HTTP response'");
                        if (req.RequestUri.AbsoluteUri == $"http://{host}:{port}/")
                            return HttpResponseFactory.GetResponse(content: "'{X-Gigya-ServerHostname: someValue}'");
                        throw new HttpRequestException("Invalid uri");
                    });

                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                 k =>
                 {
                     k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                     k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
                     var getConfig = k.Get<Func<DiscoveryConfig>>();
                     k.Rebind<Func<DiscoveryConfig>>().ToMethod(c =>
                     {
                         var config = getConfig();
                         config.UseHttpsOverride = false;
                         
                         return () => config;
                     });
                 }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;
                TracingContext.SetRequestID("1");

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                var server = await serviceProxy.Invoke(request, typeof(string));

                server.ShouldBe("some HTTP response");

                Assert.That(() => httpsTestCount, Is.EqualTo(1).After(10).Seconds.PollEvery(1).Seconds);

                server = await serviceProxy.Invoke(request, typeof(string));

                server.ShouldBe("some HTTPS response");

                server = await serviceProxy.Invoke(request, typeof(string));

                server.ShouldBe("some HTTP response");
            }
        }

        [Test]
        public async Task HttpsDisabled_NoCertificate_CallSucceeds()
        {
            var host = "host1";
            var httpsPortOffset = 5;
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", host},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()},
                {"Discovery.Services.DemoService.UseHttpsOverride", "false"}
            };

            int httpsTestCount = 0;
            bool httpsMethodCalled = false;

            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _=>
            {
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("https://*")
                    .Respond(req =>
                        HttpResponseFactory.GetResponseWithException(ExceptionSerializer, new SocketException()));

                messageHandler
                    .When("http://*")
                    .Respond(req => HttpResponseFactory.GetResponse(content: "'some HTTP response'"));

                return messageHandler;
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                 k =>
                 {
                     k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                     k.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);

                     var certificateLocator = Substitute.For<ICertificateLocator>();
                     certificateLocator
                         .When(cl => cl.GetCertificate(Arg.Any<string>()))
                         .Do(x => throw new Exception());
                     k.Rebind<ICertificateLocator>().ToConstant(certificateLocator);

                     var httpsAuthenticator = Substitute.For<IHttpsAuthenticator>();
                     httpsAuthenticator
                         .When(a => a.AddHttpMessageHandlerAuthentication(Arg.Any<HttpClientHandler>(), Arg.Any<HttpClientConfiguration>()))
                         .Do(x => throw new Exception());
                     k.Rebind<IHttpsAuthenticator>().ToConstant(httpsAuthenticator);
                 }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;
                TracingContext.SetRequestID("1");

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                await serviceProxy.Invoke(request, typeof(string));
            }
        }
    }
}
