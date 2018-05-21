using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.Testing;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.UnitTests.ServiceProxyTests;

using Metrics;
using Ninject;
using Ninject.Parameters;

using NSubstitute;

using NUnit.Framework;

using RichardSzalay.MockHttp;

using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{

    [TestFixture]
    public class HttpServiceListenerTests
    {
        private IDemoService _insecureClient;

        private TestingHost<IDemoService> _testinghost;
        private Task _stopTask;
        private JsonExceptionSerializer _exceptionSerializer;
        private TestingKernel<ConsoleLog> _kernel;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _kernel = new TestingKernel<ConsoleLog>();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _kernel?.Dispose();
        }

        [SetUp]
        public virtual void SetUp()
        {
            _insecureClient = _kernel.Get<IDemoService>();
            _exceptionSerializer = _kernel.Get<JsonExceptionSerializer>();

            Metric.ShutdownContext("Service");
            TracingContext.SetUpStorage();
            TracingContext.SetRequestID("1");

            _testinghost = new TestingHost<IDemoService>();
            _stopTask = _testinghost.RunAsync();
        }


        [TearDown]
        public virtual void TearDown()
        {
            _testinghost.Stop();
            _stopTask.Wait();
            Metric.ShutdownContext("Service");
        }



        [Test]
        public void RequestWithException_ShouldWrapAndThrow()
        {
            _testinghost.Instance.When(a => a.DoSomething()).Throw(x => new ArgumentException("MyEx"));

            var actual = _insecureClient.DoSomething().ShouldThrow<RemoteServiceException>();

            actual.InnerException.ShouldBeOfType<ArgumentException>();
            actual.InnerException.Message.ShouldBe("MyEx");
        }


        [Test]
        public async Task  RequestWithException_ShouldNotWrapWithUnhandledException()
        {
            _testinghost.Instance.When(a => a.DoSomething()).Throw(x => new ArgumentException("MyEx"));
            var request = await GetRequestFor<IDemoService>(p => p.DoSomething());

            var responseJson = await (await new HttpClient().SendAsync(request)).Content.ReadAsStringAsync();
            var responseException = _exceptionSerializer.Deserialize(responseJson);
            responseException.ShouldBeOfType<ArgumentException>();
        }


        [TestCase(typeof(ProgrammaticException))]
        [TestCase(typeof(EnvironmentException))]
        [TestCase(typeof(RequestException))]
        public async Task RequestWithException_ShouldNotWrap(Type exceptionType)
        {
            _testinghost.Instance.When(a => a.DoSomething()).Throw(i => (Exception)Activator.CreateInstance(exceptionType, "MyEx", null, null, null));
            var request = await GetRequestFor<IDemoService>(p => p.DoSomething());

            var responseJson = await (await new HttpClient().SendAsync(request)).Content.ReadAsStringAsync();
            var responseException = _exceptionSerializer.Deserialize(responseJson);
            responseException.ShouldBeOfType(exceptionType);
        }


        [Test]
        public async Task SendRequestWithInt32Parameter_ShouldSucceed()
        {
            _testinghost.Instance.IncrementInt(Arg.Any<int>())
                       .Returns(info => info.Arg<int>() + 1);

            var res = await _insecureClient.IncrementInt(0);
            res.Should().Be(1);

            await  _testinghost.Instance.Received().IncrementInt(0);
        }


        [Test]
        public async Task SendRequestWithInt64Parameter_ShouldSucceed()
        {
            _testinghost.Instance
                       .Increment(Arg.Any<ulong>())
                       .Returns(info => info.Arg<ulong>() + 1);

            var res = await _insecureClient.Increment(0);
            res.Should().Be(1);

            ulong maxLongPlusOne = (ulong)long.MaxValue + 1;

            res = await _insecureClient.Increment(maxLongPlusOne);
            res.Should().Be(maxLongPlusOne + 1);

            await _testinghost.Instance.Received().Increment(0);
        }


        [Test]
        public async Task SendRequestWithNullParameter()
        {
            _testinghost.Instance.ToUpper(null).Returns((string)null);
            var res = await _insecureClient.ToUpper(null);
            res.Should().BeNullOrEmpty();
            await _testinghost.Instance.Received().ToUpper(null);
        }


        [Test]
        public async Task SendRequestWithNoParameters()
        {
            await _insecureClient.DoSomething();
            await _testinghost.Instance.Received().DoSomething();
        }


        [Test]
        public async Task  SendRequestWithEnumParameter()
        {
            await _insecureClient.SendEnum(TestEnum.Enval1);
            await _testinghost.Instance.Received().SendEnum(TestEnum.Enval1);
        }


        private  async Task< HttpRequestMessage> GetRequestFor<T>(Func<T, Task> action)
        {
            HttpRequestMessage request = null;
            string requestContent = null;
            var mockHandler = new MockHttpMessageHandler();
            mockHandler.When("*").Respond(async r =>
            {
                request = r;
                requestContent = await r.Content.ReadAsStringAsync();
                return HttpResponseFactory.GetResponse(content: "''");
            });
            var kernel = new TestingKernel<ConsoleLog>();
            var client = kernel
                .Get<ServiceProxyProviderSpy<T>>(new ConstructorArgument("httpMessageHandler", mockHandler))
                .Client;

            await action(client);

            var contentClone = new StringContent(requestContent, Encoding.UTF8, "application/json");

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers.Where(h => h.Key.StartsWith("X")))
                contentClone.Headers.Add(header.Key, header.Value);

            kernel.Dispose();

            return new HttpRequestMessage(request.Method, request.RequestUri) { Content = contentClone };
        }

    }


}
