using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using FluentAssertions;

using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.Testing;
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

            var kernel = new TestingKernel<ConsoleLog>(k => k.Rebind<IDiscoverySourceLoader>().To<DiscoverySourceLoader>().InSingletonScope(), dict);            
            var providerFactory = kernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory("DemoService");

          

            var messageHandler = new MockHttpMessageHandler();            
            messageHandler
                .When("*")
                .Respond(req=> HttpResponseFactory.GetResponse(content: $"'{req.RequestUri.Host}'"));

            serviceProxy.HttpMessageHandler = messageHandler;

            //If we set Request Id we would like always to select same Host
            TracingContext.SetRequestID("dumyId1");
            var request = new HttpServiceRequest("testMethod", new Dictionary<string, object>());
            var hostOfFirstReq = (string)await serviceProxy.Invoke(request, typeof(string));
            string host;
            for(int i = 0;i < 50;i++)
            {
                host = (string)await serviceProxy.Invoke(request, typeof(string));
                host.ShouldBe(hostOfFirstReq);
            }

            TracingContext.SetRequestID("dumyId2");
            host = (string)await serviceProxy.Invoke(request, typeof(string));           
            host.ShouldNotBe(hostOfFirstReq);
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
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(expected));

            Func<Task> action = async () => await CreateClient(messageHandler).ToUpper("aaaa");

            action.ShouldThrow<RequestException>().Message.Should().Be(expected.Message);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithCustomerFacingException_CorrectExceptionIsThrown()
        {
            var expected = new RequestException("You action is invalid, Mr. Customer.", 30000).ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(expected));

            var actual = CreateClient(messageHandler).ToUpper("aaaa").ShouldThrow<RequestException>();

            actual.Message.ShouldBe(expected.Message);
            actual.ErrorCode.ShouldBe(expected.ErrorCode);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithEnvironmentException_CorrectExceptionIsThrown()
        {
            var expected = new EnvironmentException("You environment is invalid.").ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(expected));

            var actual = CreateClient(messageHandler).ToUpper("aaaa").ShouldThrow<EnvironmentException>();

            actual.Message.ShouldBe(expected.Message);
        }

        [Test]
        public async Task ToUpper_MethodCallFailsWithRemoteServiceException_CorrectExceptionIsThrown()
        {
            var expected = new RemoteServiceException("A service is invalid.", "someUri").ThrowAndCatch();
            var messageHandler = new MockHttpMessageHandler();
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(expected));

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
            messageHandler.When("*").Respond(HttpResponseFactory.GetResponseWithException(expected));

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
