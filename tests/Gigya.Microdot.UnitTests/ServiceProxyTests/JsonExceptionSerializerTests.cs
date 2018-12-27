using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Utils;
using Ninject;
using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
    public class JsonExceptionSerializerTests : UpdatableConfigTests
    {
        private JsonExceptionSerializer ExceptionSerializer { get; set; }

        public override void Setup()
        {
            
        }

        public override void OneTimeSetUp()
        {
            base.OneTimeSetUp();
            ExceptionSerializer = _unitTestingKernel.Get<JsonExceptionSerializer>();
            Task t = ChangeConfig<StackTraceEnhancerSettings>(new[]
            {
                new KeyValuePair<string, string>("StackTraceEnhancerSettings.RegexReplacements.TidyAsyncLocalFunctionNames.Pattern",
                    @"\.<>c__DisplayClass(?:\d+)_(?:\d+)(?:`\d)?\.<<(\w+)>g__(\w+)\|?\d>d.MoveNext\(\)"),
                new KeyValuePair<string, string>("StackTraceEnhancerSettings.RegexReplacements.TidyAsyncLocalFunctionNames.Replacement",
                    @".$1.$2(async)")
            });
            t.Wait();
        }

        protected override Action<IKernel> AdditionalBindings()
        {
            return null;
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
        public void RequestException_Serialization_AddBreadcrumbs()
        {                        
            var json = ExceptionSerializer.Serialize(new RequestException("message").ThrowAndCatch());
            var actual = ExceptionSerializer.Deserialize(json);

            var breadcrumbs = ((RequestException)actual).Breadcrumbs;
            breadcrumbs.ShouldNotBeEmpty();
            breadcrumbs.First().ServiceName.ShouldBe("InfraTests");            
        }

        [Test]
        public void RequestException_SerializedTwice_AddAnotherBreadcrumb()
        {
            var json1 = ExceptionSerializer.Serialize(new RequestException("message").ThrowAndCatch());
            var actual1 = ExceptionSerializer.Deserialize(json1);
            var json2 = ExceptionSerializer.Serialize(actual1);
            var actual2 = ExceptionSerializer.Deserialize(json2); 

            var breadcrumbs = ((RequestException)actual2).Breadcrumbs;
            breadcrumbs.Count.ShouldBe(2);
            breadcrumbs[0].ServiceName.ShouldBe("InfraTests");
            breadcrumbs[1].ServiceName.ShouldBe("InfraTests");
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
