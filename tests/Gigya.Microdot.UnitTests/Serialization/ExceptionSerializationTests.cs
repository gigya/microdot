using System;
using System.Net.Http;


using Gigya.Common.Contracts.Exceptions;

using NUnit.Framework;

using Orleans.Serialization;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Serialization
{
    /*
    // #ORLEANS20
	[TestFixture][Parallelizable(ParallelScope.Fixtures)]
	public class ExceptionSerializationTests
	{
		private MyServiceException MyServiceException { get; set; }

		[OneTimeSetUp]
		public void SetUp()
		{
		    try
		    {
                SerializationManager.InitializeForTesting();

                MyServiceException = new MyServiceException(
                    "My message",
                    new BusinessEntity { Name = "name", Number = 5 },
                    unencrypted: new Tags { { "t1", "v1" } }).ThrowAndCatch();
            }
		    catch(Exception e)
		    {
		        Console.Write(e);
		    }			
		}

		[Test]
		public void OrleansSerialization_MyServiceException_IsEquivalent()
		{
			var actual = SerializationManager.RoundTripSerializationForTesting(MyServiceException);

			AssertExceptionsAreEqual(MyServiceException, actual);
		}

		[Test]
		public void OrleansSerialization_InnerMyServiceException_IsEquivalent()
		{
		    var expected = new Exception("Intermediate exception", MyServiceException).ThrowAndCatch();

			var actual = SerializationManager.RoundTripSerializationForTesting(new Exception("Intermediate exception", MyServiceException).ThrowAndCatch());

			AssertExceptionsAreEqual(expected, actual);
		}

        [Test]
        public void OrleansSerialization_CustomerFacingException_IsEquivalent()
        {
            var expected = new RequestException("Test",10000).ThrowAndCatch();

            var actual = SerializationManager.RoundTripSerializationForTesting(expected);

            AssertExceptionsAreEqual(expected, actual);
            expected.ErrorCode.ShouldBe(10000);
        }

        [Test]
        public void OrleansSerialization_HttpRequestException_IsEquivalent()
        {
            var expected = new HttpRequestException("HTTP request exception").ThrowAndCatch();

            var actual = SerializationManager.RoundTripSerializationForTesting(expected);

            AssertExceptionsAreEqual(expected, actual);
        }

        private static void AssertExceptionsAreEqual(Exception expected, Exception actual)
		{
			Assert.NotNull(actual);
			Assert.AreEqual(expected.GetType(), actual.GetType());
			Assert.AreEqual(expected.Message, actual.Message);
			Assert.AreEqual(expected.StackTrace, actual.StackTrace);

			if (expected is SerializableException)
			{
				var typedExpected = expected as SerializableException;
				var typedActual = actual as SerializableException;
				CollectionAssert.AreEqual(typedExpected.EncryptedTags, typedActual.EncryptedTags);
				CollectionAssert.AreEqual(typedExpected.UnencryptedTags, typedActual.UnencryptedTags);
				Assert.AreEqual(typedActual.ExtendedProperties.Count, 0);
			}

			if (expected is MyServiceException)
				Assert.AreEqual(((MyServiceException)expected).Entity, ((MyServiceException)actual).Entity);

			if (expected.InnerException != null)
				AssertExceptionsAreEqual(expected.InnerException, actual.InnerException);
		}
	}
    */
}
