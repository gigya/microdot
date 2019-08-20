using System;
using System.Net.Http;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Orleans;
using Orleans.Configuration;
using Orleans.Serialization;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Serialization
{

	[TestFixture,Parallelizable(ParallelScope.Fixtures)]
	public class ExceptionSerializationTests
    {
        private SerializationManager _serializationManager;
		private MyServiceException MyServiceException { get; set; }

        /// <remarks>
        /// c:\gigya\orleans\test\Benchmarks\Serialization\SerializationBenchmarks.cs
        /// </remarks>
        private void InitializeSerializer()
        {
            Type fallback = null; // use default

            var client = new ClientBuilder()
                .UseLocalhostClustering()
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = nameof(ExceptionSerializationTests);
                    options.ServiceId = Guid.NewGuid().ToString();
                })
                .Configure<SerializationProviderOptions>(
                    options =>
                    {
                        // Configure the same, but pointless for exceptions
                        options.SerializationProviders.Add(typeof(OrleansCustomSerialization));
                        options.SerializationProviders.Add(typeof(HttpRequestExceptionSerializer));
                        options.FallbackSerializationProvider = typeof(OrleansCustomSerialization);
                    })
                .Build();
            
            _serializationManager = client.ServiceProvider.GetRequiredService<SerializationManager>();
        }

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
		    try
		    {
                InitializeSerializer();

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
			var actual = _serializationManager.RoundTripSerializationForTesting(MyServiceException);

			AssertExceptionsAreEqual(MyServiceException, actual);
		}

		[Test]
		public void OrleansSerialization_InnerMyServiceException_IsEquivalent()
		{
		    var expected = new Exception("Intermediate exception", MyServiceException).ThrowAndCatch();

			var actual = _serializationManager.RoundTripSerializationForTesting(new Exception("Intermediate exception", MyServiceException).ThrowAndCatch());

			AssertExceptionsAreEqual(expected, actual);
		}

        [Test]
        public void OrleansSerialization_CustomerFacingException_IsEquivalent()
        {
            var expected = new RequestException("Test",10000).ThrowAndCatch();

            var actual = _serializationManager.RoundTripSerializationForTesting(expected);

            AssertExceptionsAreEqual(expected, actual);
            expected.ErrorCode.ShouldBe(10000);
        }


        /// <summary>
        /// [DONE] #ORLEANS20 - I don't now why, but round/trip for HttpRequestException is loosing stack trace...
        /// https://github.com/dotnet/orleans/issues/5876
        /// </summary>
        [Test]
        public void OrleansSerialization_HttpRequestException_IsEquivalent()
        {
            var expected = new HttpRequestException("HTTP request exception").ThrowAndCatch();

            var actual1 = (HttpRequestException)_serializationManager.DeepCopy(expected);
            AssertExceptionsAreEqual(expected, actual1);

            var actual = _serializationManager.RoundTripSerializationForTesting(expected);
            var actual2 = _serializationManager.DeserializeFromByteArray<HttpRequestException>(_serializationManager.SerializeToByteArray(expected));
            
            AssertExceptionsAreEqual(expected, actual2);
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

}
