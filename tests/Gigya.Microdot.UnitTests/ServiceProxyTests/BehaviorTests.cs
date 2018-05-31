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
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.HostManagement;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Shared;
using Newtonsoft.Json;
using Ninject;
using NUnit.Framework;

using RichardSzalay.MockHttp;

using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{

    public class BehaviorTests : AbstractServiceProxyTest
    {
        [Test]
        public async Task AllRequestsForSameCallID_SameHostSelected()
        {
            var dict = new Dictionary<string, string> {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "host1,host2"},
                {"Discovery.Services.DemoService.DefaultPort", "5555"}
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>();
                        k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                    }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");



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
        public async Task RequestContextShouldOverridePortAndHost()
        {
            const string serviceName = "DemoService";
            const int defaultPort = 5555;
            var dict = new Dictionary<string, string>
            {
                {$"Discovery.Services.{serviceName}.Source", "Config"},
                {$"Discovery.Services.{serviceName}.Hosts", "host1"},
                {$"Discovery.Services.{serviceName}.DefaultPort", defaultPort.ToString()}
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(k =>{}, dict))
            {


                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory(serviceName);



                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond(req => HttpResponseFactory.GetResponse(
                        content: $"'{req.RequestUri.Host}:{req.RequestUri.Port}'"));

                serviceProxy.HttpMessageHandler = messageHandler;
                string overrideHost = "override-host";
                int overridePort = 5318;

                TracingContext.SetHostOverride(serviceName, overrideHost, overridePort);

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());
                for (int i = 0; i < 50; i++)
                {
                    var host = (string)await serviceProxy.Invoke(request, typeof(string));
                    host.ShouldBe($"{overrideHost}:{overridePort}");
                }

            }
        }

        [Test]
        public async Task RequestContextShouldOverrideHostOnly()
        {
            const string serviceName = "DemoService";
            const int defaultPort = 5555;

            var dict = new Dictionary<string, string> {
                {$"Discovery.Services.{serviceName}.Source", "Config"},
                {$"Discovery.Services.{serviceName}.Hosts", "host1"},
                {$"Discovery.Services.{serviceName}.DefaultPort", defaultPort.ToString()}
            };

            var kernel = new TestingKernel<ConsoleLog>(k => {}, dict);
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
            var dict = new Dictionary<string, string> {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "host1,host2"},
                {"Discovery.Services.DemoService.DefaultPort", "5555"}
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>();
                        k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                    }, dict)

            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");

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
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "host1,host2"},
                {"Discovery.Services.DemoService.DefaultPort", "5555"}
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscovery>().To<ServiceDiscovery.Rewrite.Discovery>();
                        k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                    }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");

                //Disable  TracingContext.SetRequestID("1");
                CallContext.FreeNamedDataSlot("#ORL_RC");

                int counter = 0;
                var messageHandler = new MockHttpMessageHandler();
                messageHandler
                    .When("*")
                    .Respond( req =>
                    {
                        bool disableReachabilityChecker = req.Content==null;
                        if(disableReachabilityChecker) throw new HttpRequestException();

                        counter++;
                    
                        if ( req.RequestUri.Host == "host1") throw new HttpRequestException();
                        return HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}'");
                    });

                serviceProxy.HttpMessageHandler = messageHandler;

                var request = new HttpServiceRequest("testMethod", null, new Dictionary<string, object>());

                for (int i = 0; i < 3; i++)
                {
                    var server = await serviceProxy.Invoke(request, typeof(string));
                    server.ShouldBe("host2");
                }
            }
        }


        [Test]
        public async Task RequestContextOverrideShouldFailOnFirstAttempt()
        {
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "Config"},
                {"Discovery.Services.DemoService.Hosts", "notImportant"},
                {"Discovery.Services.DemoService.DefaultPort", "5555"}
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(
                    k =>
                    {
                        k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope();
                    }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");

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
                    await act.ShouldThrowAsync<ServiceUnreachableException>();
                }
                counter.ShouldBe(3);
            }
        }

        
        [Test]
        public async Task FailedHostShouldBeRemovedFromHostList()
        {
            var dict = new Dictionary<string, string>
            {
                {"Discovery.Services.DemoService.Source", "local"},
                {"Discovery.Services.DemoService.DefaultPort", "5555"}
            };

            using (var kernel =
                new TestingKernel<ConsoleLog>(k=> { }, dict)
            )
            {

                var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
                var serviceProxy = providerFactory("DemoService");

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
