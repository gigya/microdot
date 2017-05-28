using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;

using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Utils;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
    [TestFixture]
    public class JsonExceptionSerializerTests
    {
        [Test]
        public void RequestException_RoundTrip_IsIdentical()
        {
            var expected = new RequestException("message").ThrowAndCatch();
            var json = JsonExceptionSerializer.Serialize(expected);

            var actual = JsonExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RequestException>();
            actual.Message.ShouldBe(expected.Message);
            actual.StackTrace.ShouldBe(expected.StackTrace);
        }

        [Test]
        public void CustomerFacingException_RoundTrip_IsIdentical()
        {
            var expected = new RequestException("message", 30000).ThrowAndCatch();
            var json = JsonExceptionSerializer.Serialize(expected);

            var actual = (RequestException)JsonExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RequestException>();
            actual.Message.ShouldBe(expected.Message);
            actual.ErrorCode.ShouldBe(expected.ErrorCode);
            actual.StackTrace.ShouldBe(expected.StackTrace);
        }

        [Test]
        public void ExceptionTypeNotAvailable_RoundTrip_FallsBackToAvailableType()
        {
            var expected = new MyException(30000, "message") { MyNumber = 42 }.ThrowAndCatch();
            var json = JsonExceptionSerializer.Serialize(expected);

            json = json.Replace("MyException", "MyNonexistentException");
            var actual = (RequestException)JsonExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RequestException>();
            actual.Message.ShouldBe(expected.Message);
            actual.ErrorCode.ShouldBe(30000);
            actual.ExtendedProperties.Count.ShouldBe(1);
            actual.ExtendedProperties.Single().ShouldBe(new KeyValuePair<string, object>("MyNumber", 42L));
            actual.StackTrace.ShouldBe(expected.StackTrace);
        }

        [Test]
        public void HttpRequestException_RoundTrip_ReturnsSubstitue()
        {
            var ex = new HttpRequestException("message").ThrowAndCatch();
            var json = JsonExceptionSerializer.Serialize(ex);

            var actual = JsonExceptionSerializer.Deserialize(json);

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

            string json = JsonExceptionSerializer.Serialize(ex);
            var actual = JsonExceptionSerializer.Deserialize(json);

            actual.ShouldBeOfType<RemoteServiceException>();
            actual.Message.ShouldBe(ex.Message);
            actual.StackTrace.ShouldBe(ex.StackTrace);
            actual.InnerException.ShouldBeOfType<WebException>();
            actual.InnerException.Message.ShouldBe(webEx.Message);
            actual.InnerException.StackTrace.ShouldBe(webEx.StackTrace);
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
