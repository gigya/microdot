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
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.Host;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Service;
using Gigya.Microdot.UnitTests.Caching.Host;
using Gigya.Microdot.UnitTests.ServiceProxyTests;
using Metrics;
using Ninject;
using Ninject.Syntax;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

using RichardSzalay.MockHttp;

using Shouldly;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Hosting.Service;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{

    [TestFixture, Parallelizable(ParallelScope.None)]
    public class HttpServiceListenerTests
    {
        private IDemoService _insecureClient;

        private NonOrleansServiceTester<TestingHost<IDemoService>> _testinghost;



        [SetUp]
        public virtual void SetUp()
        {
            _testinghost = new NonOrleansServiceTester<TestingHost<IDemoService>>();
            _insecureClient = _testinghost.GetServiceProxy<IDemoService>();
            Metric.ShutdownContext("Service");
            TracingContext.SetRequestID("1");
        }

        [TearDown]
        public virtual void TearDown()
        {
            try
            {
                _testinghost.Dispose();
                Metric.ShutdownContext("Service");
            }
            catch
            {
                //should not fail tests
            }
        }



        [Test]
        public void RequestWithException_ShouldWrapAndThrow()
        {
            _testinghost.Host.Kernel.Get<IDemoService>().When(a => a.DoSomething()).Throw(x => new ArgumentException("MyEx"));

            var actual = _insecureClient.DoSomething().ShouldThrow<RemoteServiceException>();

            actual.InnerException.ShouldBeOfType<ArgumentException>();
            actual.InnerException.Message.ShouldBe("MyEx");
        }


        [Test]
        public async Task RequestWithException_ShouldNotWrapWithUnhandledException()
        {
            var _kernel = new TestingKernel<ConsoleLog>();
            var _exceptionSerializer = _kernel.Get<JsonExceptionSerializer>();
            _testinghost.Host.Kernel.Get<IDemoService>().When(a => a.DoSomething()).Throw(x => new ArgumentException("MyEx"));
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
            var _kernel = new MicrodotInitializer(
                "",
                new FakesLoggersModules(), 
                k => k.RebindForTests());

            var _exceptionSerializer = _kernel.Kernel.Get<JsonExceptionSerializer>();
            _testinghost.Host.Kernel.Get<IDemoService>().When(a => a.DoSomething()).Throw(i =>
                (Exception) Activator.CreateInstance(exceptionType, "MyEx", null, null, null));

            var request = await GetRequestFor<IDemoService>(p => p.DoSomething());

            var responseJson = await (await new HttpClient().SendAsync(request)).Content.ReadAsStringAsync();
            var responseException = _exceptionSerializer.Deserialize(responseJson);
            responseException.ShouldBeOfType(exceptionType);
        }


        [Test]
        public async Task SendRequestWithInt32Parameter_ShouldSucceed()
        {
            _testinghost.Host.Kernel.Get<IDemoService>().IncrementInt(Arg.Any<int>())
                .Returns(info => info.Arg<int>() + 1);

            var res = await _insecureClient.IncrementInt(0);
            res.Should().Be(1);

            await _testinghost.Host.Kernel.Get<IDemoService>().Received().IncrementInt(0);
        }


        [Test]
        public async Task SendRequestWithInt64Parameter_ShouldSucceed()
        {
            _testinghost.Host.Kernel.Get<IDemoService>()
                .Increment(Arg.Any<ulong>())
                .Returns(info => info.Arg<ulong>() + 1);

            var res = await _insecureClient.Increment(0);
            res.Should().Be(1);

            ulong maxLongPlusOne = (ulong) long.MaxValue + 1;

            res = await _insecureClient.Increment(maxLongPlusOne);
            res.Should().Be(maxLongPlusOne + 1);

            await _testinghost.Host.Kernel.Get<IDemoService>().Received().Increment(0);
        }


        [Test]
        public async Task SendRequestWithNullParameter()
        {
            _testinghost.Host.Kernel.Get<IDemoService>().ToUpper(null).Returns((string) null);
            var res = await _insecureClient.ToUpper(null);
            res.Should().BeNullOrEmpty();
            await _testinghost.Host.Kernel.Get<IDemoService>().Received().ToUpper(null);
        }

        [Test]
        [Ignore("should refactor to be simple ")]
        public async Task SendRequestWithInvalidParameterValue()
        {

            // var methodName = nameof(IDemoService.ToUpper);
            // var expectedParamName = typeof(IDemoService).GetMethod(methodName).GetParameters().First().Name;

            //_testinghost.Host._overrideServiceMethod = invocationTarget =>
            // {
            //     // Cause HttpServiceListener to think it is a weakly-typed request,
            //     // and get the parameters list from the mocked ServiceMethod, and not from the original invocation target
            //     invocationTarget.ParameterTypes = null;

            //     // return a ServiceMethod which expects only int values
            //     return new ServiceMethod(typeof(IDemoServiceSupportOnlyIntValues),
            //         typeof(IDemoServiceSupportOnlyIntValues).GetMethod(methodName));
            // };

            // try
            // {
            //     await _insecureClient.ToUpper("Non-Int value");
            //     Assert.Fail("Host was expected to throw an exception");
            // }
            // catch (InvalidParameterValueException ex)
            // {
            //     ex.parameterName.ShouldBe(expectedParamName);
            // }
        }


        [Test]
        public async Task SendRequestWithNoParameters()
        {
            await _insecureClient.DoSomething();
            await _testinghost.Host.Kernel.Get<IDemoService>().Received().DoSomething();
        }


        [Test]
        public async Task SendRequestWithEnumParameter()
        {
            await _insecureClient.SendEnum(TestEnum.Enval1);
            await _testinghost.Host.Kernel.Get<IDemoService>().Received().SendEnum(TestEnum.Enval1);
        }


        private async Task<HttpRequestMessage> GetRequestFor<T>(Func<T, Task> action)
        {
            HttpRequestMessage request = null;
            string requestContent = null;
            Func<HttpClientConfiguration, HttpMessageHandler> messageHandlerFactory = _ =>
            {
                var mockHandler = new MockHttpMessageHandler();
                mockHandler.When("*").Respond(async r =>
                {
                    if (r.Method != HttpMethod.Get)
                    {
                        request = r;
                        requestContent = await r.Content.ReadAsStringAsync();
                    }

                    return HttpResponseFactory.GetResponse(content: "''");
                });

                return mockHandler;
            };
            var kernel = new TestingKernel<ConsoleLog>();
            kernel.Rebind<Func<HttpClientConfiguration, HttpMessageHandler>>().ToMethod(c => messageHandlerFactory);
            var client = kernel.Get<ServiceProxyProviderSpy<T>>();

            client.DefaultPort = _testinghost.BasePort;

            await action(client.Client);

            var contentClone = new StringContent(requestContent, Encoding.UTF8, "application/json");

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers.Where(h =>
                h.Key.StartsWith("X")))
                contentClone.Headers.Add(header.Key, header.Value);

            kernel.Dispose();

            return new HttpRequestMessage(request.Method, request.RequestUri) {Content = contentClone};
        }

        /// <summary>
        /// this class simulates a version of IDemoService, which defines an incorrect parameter type for ToUpper method
        /// </summary>
        [HttpService(5551)]
        interface IDemoServiceSupportOnlyIntValues
        {
            Task<string> ToUpper(int str); // the real IDemoService accepts any string value, not only int types            
        }
    }

    [TestFixture, Parallelizable(ParallelScope.Fixtures)]
    public class HttpServiceListenerHttpsTests
    {
        [Test]
        public async Task CreateListener_HttpsDisabled_CreationSucceedsNoNeedForCertificate()
        {
            var endpointDefinition = Substitute.For<IServiceEndPointDefinition>();
            using (var httpPort = DisposablePort.GetPort())
            {
                endpointDefinition.HttpsPort.Returns(ci => httpPort.Port);
                endpointDefinition.HttpsPort.Returns(ci => null); // HTTPS disabled by a null HTTPS port

                var certificateLocator = Substitute.For<ICertificateLocator>();
                certificateLocator.GetCertificate(Arg.Any<string>()).Throws<Exception>();

                using (var listener = new HttpServiceListener(Substitute.For<IActivator>(),
                    Substitute.For<IWorker>(),
                    endpointDefinition,
                    certificateLocator,
                    Substitute.For<ILog>(),
                    Enumerable.Empty<ICustomEndpoint>(),
                    Substitute.For<IEnvironment>(),
                    new JsonExceptionSerializer(Substitute.For<IStackTraceEnhancer>(), new JsonExceptionSerializationSettings(()=> new ExceptionSerializationConfig(false, false))),
                    new ServiceSchema(),
                    () => new LoadShedding(),
                    Substitute.For<IServerRequestPublisher>(),
                    new CurrentApplicationInfo(
                        nameof(HttpServiceListenerTests),
                        Environment.UserName,
                        System.Net.Dns.GetHostName()),
                    () => new MicrodotHostingConfig()
                ))
                {
                    listener.Start();
                }

                certificateLocator.DidNotReceive().GetCertificate(Arg.Any<string>());
            }
        }

        [Test]
        public async Task CreateListener_HttpsEnabled_CreationFailsNoCertificateFound()
        {
            var endpointDefinition = Substitute.For<IServiceEndPointDefinition>();

            using (var httpPort = DisposablePort.GetPort())
            using (var httpsPort = DisposablePort.GetPort())
            {
                endpointDefinition.HttpPort.Returns(ci => httpPort.Port);
                endpointDefinition.HttpsPort.Returns(ci => httpsPort.Port); // HTTPS enabled by a non-null HTTPS port
                endpointDefinition.ClientCertificateVerification.Returns(ci =>
                    ClientCertificateVerificationMode.VerifyIdenticalRootCertificate);

                var certificateLocator = Substitute.For<ICertificateLocator>();
                certificateLocator.GetCertificate(Arg.Any<string>()).Throws<Exception>();

                Assert.Throws<Exception>(() => new HttpServiceListener(Substitute.For<IActivator>(),
                    Substitute.For<IWorker>(),
                    endpointDefinition,
                    certificateLocator,
                    Substitute.For<ILog>(),
                    Enumerable.Empty<ICustomEndpoint>(),
                    Substitute.For<IEnvironment>(),
                    new JsonExceptionSerializer(Substitute.For<IStackTraceEnhancer>(), new JsonExceptionSerializationSettings(()=> new ExceptionSerializationConfig(false, false))),
                    new ServiceSchema(),
                    () => new LoadShedding(),
                    Substitute.For<IServerRequestPublisher>(),
                    new CurrentApplicationInfo(
                        nameof(HttpServiceListenerTests),
                        Environment.UserName,
                        System.Net.Dns.GetHostName()),
                    () => new MicrodotHostingConfig()));

                certificateLocator.Received(1).GetCertificate("Service");
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task CallService_ClientHttpsConfiguration_ShouldSucceed(bool httpsEnabledInClient)
        {
            var testingHost = new NonOrleansServiceTester<SlowServiceHost>();
            if (!httpsEnabledInClient)
                testingHost.CommunicationKernel.DisableHttps();

            var client = testingHost.GetServiceProxy<ISlowService>();
            await client.SimpleSlowMethod(1, 2);
        }

        public class SlowServiceHost : MicrodotServiceHost<ISlowService>
        {
            public override string ServiceName => nameof(ISlowService).Substring(1);

            protected override ILoggingModule GetLoggingModule()
            {
                return new ConsoleLogLoggersModules();
            }

            protected override void PreConfigure(IKernel kernel, ServiceArguments Arguments)
            {
                var env = new HostEnvironment(new TestHostEnvironmentSource(this.ServiceName));
                kernel.Rebind<IEnvironment>().ToConstant(env).InSingletonScope();
                kernel.Rebind<CurrentApplicationInfo>().ToConstant(env.ApplicationInfo).InSingletonScope();

                base.PreConfigure(kernel, Arguments);
            }

            protected override void Configure(IKernel kernel, BaseCommonConfig commonConfig)
            {
                kernel.Bind<ISlowService>().To<SlowService>().InSingletonScope();
                kernel.RebindForTests();
            }
        }
    }

    static class HttpsConfigurationHelper
    {
        public static void DisableHttps(this IResolutionRoot resolutionRoot)
        {
            DiscoveryConfig getDiscoveryConfig = resolutionRoot.Get<Func<DiscoveryConfig>>()();
            getDiscoveryConfig.Services["SlowService"].UseHttpsOverride = false;
        }
    }
}
