//using System;
//using System.Net.Http;


//using Gigya.Common.Contracts.Exceptions;
//using NSubstitute;
//using NUnit.Framework;
//using Orleans.Runtime;
//using Orleans.Runtime.Configuration;
//using Orleans.Serialization;
//using Shouldly;

//namespace Gigya.Microdot.UnitTests.Serialization
//{
//    [TestFixture]
//    public class ExceptionSerializationTests
//    {
//        private SerializationManager _serializer;
//        private MyServiceException MyServiceException { get; set; }

//        [OneTimeSetUp]
//        public void SetUp()
//        {
//            try
//            {
//                var config = Substitute.For<IMessagingConfiguration>();
//                var trace = Substitute.For<ITraceConfiguration>();
//                //BufferPool.GlobalPool = null;
//                config.FallbackSerializationProvider = null;
//                config.BufferPoolBufferSize.Returns(30000);
//                config.BufferPoolMaxSize = 2;
//                config.BufferPoolPreallocationSize = 3;
//                _serializer = new SerializationManager(serviceProvider: null, config: config, traceConfig: trace);


//                MyServiceException = new MyServiceException(
//                    "My message",
//                    new BusinessEntity { Name = "name", Number = 5 },
//                    unencrypted: new Tags { { "t1", "v1" } }).ThrowAndCatch();
//            }
//            catch (Exception e)
//            {
//                Console.Write(e);
//            }
//        }

//        [Test]
//        public void OrleansSerialization_MyServiceException_IsEquivalent()
//        {
//            var actual = _serializer.RoundTripSerializationForTesting(MyServiceException);

//            AssertExceptionsAreEqual(MyServiceException, actual);
//        }

//        [Test]
//        public void OrleansSerialization_InnerMyServiceException_IsEquivalent()
//        {
//            var expected = new Exception("Intermediate exception", MyServiceException).ThrowAndCatch();

//            var actual = _serializer.RoundTripSerializationForTesting(new Exception("Intermediate exception", MyServiceException).ThrowAndCatch());

//            AssertExceptionsAreEqual(expected, actual);
//        }

//        [Test]
//        public void OrleansSerialization_CustomerFacingException_IsEquivalent()
//        {
//            var expected = new RequestException("Test", 10000).ThrowAndCatch();

//            var actual = _serializer.RoundTripSerializationForTesting(expected);

//            AssertExceptionsAreEqual(expected, actual);
//            expected.ErrorCode.ShouldBe(10000);
//        }

//        [Test]
//        public void OrleansSerialization_HttpRequestException_IsEquivalent()
//        {
//            var expected = new HttpRequestException("HTTP request exception").ThrowAndCatch();

//            var actual = _serializer.RoundTripSerializationForTesting(expected);

//            AssertExceptionsAreEqual(expected, actual);
//        }

//        private static void AssertExceptionsAreEqual(Exception expected, Exception actual)
//        {
//            Assert.NotNull(actual);
//            Assert.AreEqual(expected.GetType(), actual.GetType());
//            Assert.AreEqual(expected.Message, actual.Message);
//            Assert.AreEqual(expected.StackTrace, actual.StackTrace);

//            if (expected is SerializableException)
//            {
//                var typedExpected = expected as SerializableException;
//                var typedActual = actual as SerializableException;
//                CollectionAssert.AreEqual(typedExpected.EncryptedTags, typedActual.EncryptedTags);
//                CollectionAssert.AreEqual(typedExpected.UnencryptedTags, typedActual.UnencryptedTags);
//                Assert.AreEqual(typedActual.ExtendedProperties.Count, 0);
//            }

//            if (expected is MyServiceException)
//                Assert.AreEqual(((MyServiceException)expected).Entity, ((MyServiceException)actual).Entity);

//            if (expected.InnerException != null)
//                AssertExceptionsAreEqual(expected.InnerException, actual.InnerException);
//        }
//    }
//}
