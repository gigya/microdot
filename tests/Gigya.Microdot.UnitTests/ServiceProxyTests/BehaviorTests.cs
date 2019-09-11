using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using FluentAssertions;

using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
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
        [Test]
        public async Task AllRequestsForSameCallID_SameHostSelected()
        {
            var port = DisposablePort.GetPort().Port;
            var dict = new Dictionary<string, string> {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "host1,host2"},
                {"Discovery.Services.DemoService.DefaultPort", port.ToString()}
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k => k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope(), dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;

                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req => HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}'"));

                serviceProxy.HttpMessageHandler = messageHandler;

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

            using (var kernel = new TestingKernel<ConsoleLog>(k => k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope(), dict))
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();

                TracingContext.SetRequestID("g"); 
                
                var serviceProxy = providerFactory(serviceName);
                Uri uri = null;
                string requestMessage = null;
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*").Respond(async req =>
                    {
                        requestMessage = await req.Content.ReadAsStringAsync();
                        uri = req.RequestUri;
                        return HttpResponseFactory.GetResponse(HttpStatusCode.Accepted);
                    });

                serviceProxy.HttpMessageHandler = messageHandler;
                string expectedHost = "override-host";
                int expectedPort = DisposablePort.GetPort().Port;

                TracingContext.SetHostOverride(serviceName, expectedHost, expectedPort);

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());
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

            var kernel = new TestingKernel<ConsoleLog>(k => k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope(), dict);
            var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory(serviceName);

            var messageHandler = new MockHttpMessageHandler();
            messageHandler
                .When("*")
                .Respond(req => HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}:{req.RequestUri.Port}'"));
            string overrideHost = "override-host";

            serviceProxy.HttpMessageHandler = messageHandler;

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

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                    }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;
                int counter = 0;
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {
                        bool disableReachabilityChecker = req.Content == null;
                        if (disableReachabilityChecker) throw new HttpRequestException();

                        counter++;
                        //HttpRequestException
                        throw new HttpRequestException();
                    });

                serviceProxy.HttpMessageHandler = messageHandler;

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

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                 k =>
                 {
                     k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope();
                 }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;
                TracingContext.SetRequestID("1");

        int counter = 0;
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {

                        counter++;

                        if (req.RequestUri.Host == "host1") throw new HttpRequestException();
                        return HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}'");
                    });

                serviceProxy.HttpMessageHandler = messageHandler;

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

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k => k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope(), dict)
            )
            {
                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;

                //Disable  TracingContext.SetRequestID("1");

                CallContext.FreeNamedDataSlot("#ORL_RC");

                int counter = 0;
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req =>
                    {
                        counter++;

                        throw new HttpRequestException();
                    });


                string overrideHost = "override-host";
                int overridePort = 5318;
                TracingContext.SetHostOverride("DemoService", overrideHost, overridePort);
                serviceProxy.HttpMessageHandler = messageHandler;

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

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k => k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>().InSingletonScope(), dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");
                serviceProxy.DefaultPort = port;

                //Disable  TracingContext.SetRequestID("1");

                CallContext.FreeNamedDataSlot("#ORL_RC");

                int counter = 0;
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


                serviceProxy.HttpMessageHandler = messageHandler;

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
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponse(content: $"'{expected}'"));

            var actual = await CreateClient(messageHandler).ToUpper("aaaa");

            actual.ShouldBe(expected);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithRequestException_CorrectExceptionIsThrown()
        {
            var expected = new RequestException("You request is invalid.").ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(unitTesting.Get<JsonExceptionSerializer>(), expected));

            Func<Task> action = async () => await CreateClient(messageHandler).ToUpper("aaaa");

            action.ShouldThrow<RequestException>().Message.Should().Be(expected.Message);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithCustomerFacingException_CorrectExceptionIsThrown()
        {
            var expected = new RequestException("You action is invalid, Mr. Customer.", 30000).ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected));

            var actual = CreateClient(messageHandler).ToUpper("aaaa").ShouldThrow<RequestException>();

            actual.Message.ShouldBe(expected.Message);
            actual.ErrorCode.ShouldBe(expected.ErrorCode);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithEnvironmentException_CorrectExceptionIsThrown()
        {
            var expected = new EnvironmentException("You environment is invalid.").ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected));

            var actual = CreateClient(messageHandler).ToUpper("aaaa").ShouldThrow<EnvironmentException>();

            actual.Message.ShouldBe(expected.Message);
        }




        [Test]
        public async Task ToUpper_MethodCallFailsWithRemoteServiceException_CorrectExceptionIsThrown()
        {
            var expected = new RemoteServiceException("A service is invalid.", "someUri").ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected));

            var actual = CreateClient(messageHandler).ToUpper("aaaa").ShouldThrow<RemoteServiceException>();

            actual.Message.ShouldBe(expected.Message);
            actual.RequestedUri.ShouldBe(expected.RequestedUri);
            actual.InnerException.ShouldBeNull();
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithProgrammaticException_CorrectExceptionIsThrown()
        {
            var expected = new ProgrammaticException("You code is invalid.").ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(ExceptionSerializer, expected));

            var actual = CreateClient(messageHandler).ToUpper("aaaa").ShouldThrow<RemoteServiceException>();

            actual.InnerException.ShouldBeOfType<ProgrammaticException>();
            actual.InnerException.Message.ShouldBe(expected.Message);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithInvalidJson_CorrectExceptionIsThrown()
        {
            string badJson = "not JSON!";
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponse(HttpStatusCode.InternalServerError, content: badJson));

            var actual = CreateClient(messageHandler).ToUpper("aaaa").ShouldThrow<RemoteServiceException>();

            actual.EncryptedTags["responseContent"].ShouldBe(badJson);
            actual.InnerException.ShouldBeAssignableTo<JsonException>();
        }
    }
}
