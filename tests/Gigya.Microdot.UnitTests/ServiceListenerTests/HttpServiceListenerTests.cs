using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;

using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.UnitTests.ServiceProxyTests;
using Gigya.ServiceContract.Exceptions;
using Metrics;
using Ninject;
using Ninject.Parameters;

using NSubstitute;
using NUnit.Framework;

using RichardSzalay.MockHttp;

using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{

    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class HttpServiceListenerTests
    {
        private IDemoService _insecureClient;

        private TestingHost<IDemoService> _testinghost;
        private JsonExceptionSerializer _exceptionSerializer;
        private TestingKernel<ConsoleLog> _kernel;
        private Func<InvocationTarget, ServiceMethod> _overrideServiceMethod;

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
            TracingContext tracingContext = _kernel.Get<TracingContext>();

            Metric.ShutdownContext("Service");
            tracingContext.SetRequestID("1");

            _testinghost = new TestingHost<IDemoService>(onInitialize: kernel =>
                {
                    OverrideServiceEndpointDefinition(kernel);
                }
            );
          Task.Run(()=>_testinghost.Run(new ServiceArguments(ServiceStartupMode.CommandLineNonInteractive)));
          _testinghost.WaitForServiceStartedAsync().Wait(10000);
        }

        private void OverrideServiceEndpointDefinition(IKernel kernel)
        {
            _overrideServiceMethod = null;

            var orig = kernel.Get<IServiceEndPointDefinition>();
            var mock = Substitute.For<IServiceEndPointDefinition>();
            mock.Resolve(Arg.Any<InvocationTarget>()).Returns(c =>
            {
                var invocationTarget = c.Arg<InvocationTarget>();
                return _overrideServiceMethod != null ? _overrideServiceMethod(invocationTarget) : orig.Resolve(invocationTarget);
            });
            mock.HttpPort.Returns(orig.HttpPort);
            kernel.Rebind<IServiceEndPointDefinition>().ToConstant(mock);
        }


        [TearDown]
        public virtual void TearDown()
        {
            _testinghost.Stop();
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
        public async Task SendRequestWithInvalidParameterValue()
        {
            var methodName = nameof(IDemoService.ToUpper);
            var expectedParamName = typeof(IDemoService).GetMethod(methodName).GetParameters().First().Name;

            _overrideServiceMethod = invocationTarget =>
            {
                // Cause HttpServiceListener to think it is a weakly-typed request,
                // and get the parameters list from the mocked ServiceMethod, and not from the original invocation target
                invocationTarget.ParameterTypes = null; 
                
                // return a ServiceMethod which expects only int values
                return new ServiceMethod(typeof(IDemoServiceSupportOnlyIntValues),
                    typeof(IDemoServiceSupportOnlyIntValues).GetMethod(methodName));
            };

            try
            {
                await _insecureClient.ToUpper("Non-Int value");
                Assert.Fail("Host was expected to throw an exception");
            }
            catch (InvalidParameterValueException ex)
            {
                ex.parameterName.ShouldBe(expectedParamName);
            }
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

        /// <summary>
        /// this class simulates a version of IDemoService, which defines an incorrect parameter type for ToUpper method
        /// </summary>
        [HttpService(5555)]
        interface IDemoServiceSupportOnlyIntValues
        {
            Task<string> ToUpper(int str); // the real IDemoService accepts any string value, not only int types            
        }
    }


}
