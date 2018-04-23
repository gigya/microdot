using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Utils;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Helpers;
using Ninject;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
    [TestFixture]
    public class JsonExceptionSerializerTests
    {
        private JsonExceptionSerializer ExceptionSerializer { get; set; }

        [SetUp]
        public void SetUp()
        {
            var unitTesting = new TestingKernel<ConsoleLog>(k => 
                k.Rebind<Func<StackTraceEnhancerSettings>>().ToConstant<Func<StackTraceEnhancerSettings>>(() => new StackTraceEnhancerSettings
                {
                    RegexReplacements = new Dictionary<string, RegexReplace>
                    {
                        ["TidyAsyncLocalFunctionNames"] = new RegexReplace
                        {
                            Pattern = @"\.<>c__DisplayClass(?:\d+)_(?:\d+)(?:`\d)?\.<<(\w+)>g__(\w+)\|?\d>d.MoveNext\(\)",
                            Replacement = @".$1.$2(async)"
                        }
                    }
                })
            );
            ExceptionSerializer = unitTesting.Get<JsonExceptionSerializer>();
        }

        [Test]
        public void RequestException_RoundTrip_IsIdentical()
        {
            var expected = new RequestException("message").ThrowAndCatch();
            var json = ExceptionSerializer.Serialize(expected);

            var actual = ExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RequestException>();
            actual.Message.ShouldBe(expected.Message);
            actual.StackTrace.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void CustomerFacingException_RoundTrip_IsIdentical()
        {
            var expected = new RequestException("message", 30000).ThrowAndCatch();
            var json = ExceptionSerializer.Serialize(expected);

            var actual = (RequestException)ExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RequestException>();
            actual.Message.ShouldBe(expected.Message);
            actual.ErrorCode.ShouldBe(expected.ErrorCode);
            actual.StackTrace.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void ExceptionTypeNotAvailable_RoundTrip_FallsBackToAvailableType()
        {
            var expected = new MyException(30000, "message") { MyNumber = 42 }.ThrowAndCatch();
            var json = ExceptionSerializer.Serialize(expected);

            json = json.Replace("MyException", "MyNonexistentException");
            var actual = (RequestException)ExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RequestException>();
            actual.Message.ShouldBe(expected.Message);
            actual.ErrorCode.ShouldBe(30000);
            actual.ExtendedProperties.Count.ShouldBe(1);
            actual.ExtendedProperties.Single().ShouldBe(new KeyValuePair<string, object>("MyNumber", 42L));
            actual.StackTrace.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void HttpRequestException_RoundTrip_ReturnsSubstitue()
        {
            var ex = new HttpRequestException("message").ThrowAndCatch();
            var json = ExceptionSerializer.Serialize(ex);

            var actual = ExceptionSerializer.Deserialize(json);

            var envException = actual.ShouldBeOfType<EnvironmentException>();
            envException.RawMessage().ShouldEndWith(ex.RawMessage());
            envException.UnencryptedTags["originalStackTrace"].ShouldBe(ex.StackTrace);
        }

        [Test]
        public void InnerHttpRequestException_RoundTrip_IsStripped()
        {
            var webEx = new WebException("Web exception").ThrowAndCatch();
            var httpEx = new HttpRequestException("HTTP request exception", webEx).ThrowAndCatch();
            var ex = new RemoteServiceException("Remote service exception", "http://foo/bar", httpEx).ThrowAndCatch();

            string json = ExceptionSerializer.Serialize(ex);
            var actual = ExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RemoteServiceException>();
            actual.Message.ShouldBe(ex.Message);
            actual.StackTrace.ShouldNotBeNullOrEmpty();
            actual.InnerException.ShouldBeOfType<WebException>();
            actual.InnerException.Message.ShouldBe(webEx.Message);
            actual.InnerException.StackTrace.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void InnerException_RoundTrip_AllStackTracesCleaned()
        {
            var webEx = new WebException("Web exception").ThrowAndCatchAsync();
            var ex = new RemoteServiceException("Remote service exception", "http://foo/bar", webEx).ThrowAndCatchAsync();

            string json = ExceptionSerializer.Serialize(ex);
            var actual = ExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RemoteServiceException>();
            actual.Message.ShouldBe(ex.Message);
            actual.StackTrace.ShouldNotBeNullOrEmpty();
            actual.StackTrace.ShouldNotContain("at System.Runtime");
            actual.StackTrace.ShouldNotContain("End of stack trace from previous location");
            actual.StackTrace.ShouldNotContain("__");
            actual.InnerException.ShouldBeOfType<WebException>();
            actual.InnerException.Message.ShouldBe(webEx.Message);
            actual.InnerException.StackTrace.ShouldNotBeNullOrEmpty();
            actual.InnerException.StackTrace.ShouldNotContain("at System.Runtime");
            actual.InnerException.StackTrace.ShouldNotContain("End of stack trace");
            actual.InnerException.StackTrace.ShouldNotContain("__");
        }
    }

    [Serializable]
    public class MyException : RequestException
    {
        public ushort MyNumber { get; set; }

        public MyException(int error, string message = null, Exception innerException = null) : base( message, error, innerException) { }

        protected MyException(SerializationInfo info, StreamingContext context) : base(info, context) { }        
    }
}
