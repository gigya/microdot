﻿using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Security;
using Newtonsoft.Json;
using Ninject;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Gigya.Microdot.UnitTests.Serialization
{

    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
	public class ExceptionSerializationTests
    {
	    private MyServiceException MyServiceException { get; set; }

        private class ForSerializationClass
        {
            public int Dummy;
        }


        [OneTimeSetUp]
		public void OneTimeSetUp()
		{
		    try
		    {
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
        public void ExcludeTypesSerializationBinder_PreventsConfiguredTypes()
        {
            var forbidenJson =
                @"{""$type"":""System.Windows.Data.ObjectDataProvider, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"",""MethodName"":""Start"",""MethodParameters"":{""$type"":""System.Collections.ArrayList, mscorlib, Version=4.0.0.0,Culture=neutral, PublicKeyToken=b77a5c561934e089"",""$values"":[""cmd"",""/ccalc""]},""ObjectInstance"":{""$type"":""System.Diagnostics.Process, System,Version=4.0.0.0, ulture=neutral, PublicKeyToken=b77a5c561934e089""}}";
            
            try
            {
                JsonConvert.DeserializeObject(forbidenJson, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    SerializationBinder = 
	                    new ExceptionHierarchySerializationBinder(
		                    new MicrodotTypePolicySerializationBinder(
			                    new MicrodotSerializationConstraints(()=> 
				                    new MicrodotSerializationSecurityConfig
				                    {
					                    DeserializationForbiddenTypes = new List<string>(new [] {"System.Windows.Data.ObjectDataProvider"})
					                    
				                    }
			                    )
			                )
		                )
                });
            }
            catch (Exception ex)
            {
                Assert.AreEqual("JSON Serialization Binder forbids BindToType type 'System.Windows.Data.ObjectDataProvider'", ex.InnerException?.Message);
                return;
            }
            Assert.True(false, "Json Deserialize MUST throw here (security issue)");
        }

        [Test]
        public void ExcludeTypesSerializationBinder_AllowNonConfiguredTypes()
        {
            var newCls = new ForSerializationClass();
            var json = JsonConvert.SerializeObject(newCls, new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All});

            var excludeTypesSerializationBinder =
	            new ExceptionHierarchySerializationBinder(
		            new MicrodotTypePolicySerializationBinder(
			            new MicrodotSerializationConstraints(() =>
				            new MicrodotSerializationSecurityConfig
				            {
					            DeserializationForbiddenTypes = new List<string>(new [] 
						            {"System.Windows.Data.ObjectDataProvider"}
					            )
				            }
			            )
		            )
	            );
	            

            var t = JsonConvert.DeserializeObject(json, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    SerializationBinder = excludeTypesSerializationBinder
                });

            Assert.AreEqual(typeof(ForSerializationClass), t?.GetType());

            excludeTypesSerializationBinder =
	            new ExceptionHierarchySerializationBinder(
		            new MicrodotTypePolicySerializationBinder(
			            new MicrodotSerializationConstraints(() =>
				            new MicrodotSerializationSecurityConfig
				            {
					            DeserializationForbiddenTypes = new List<string>(new [] 
						            {"System.Windows.Data.ObjectDataProvider",
						            "ForSerial"}
					            )
						    }
			            )
		            )
	            );    
	            
	            
            try
            {
                JsonConvert.DeserializeObject(json, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    SerializationBinder = excludeTypesSerializationBinder
                });
            }
            catch (Exception ex)
            {
                Assert.AreEqual("JSON Serialization Binder forbids BindToType type 'Gigya.Microdot.UnitTests.Serialization.ExceptionSerializationTests+ForSerializationClass'", ex.InnerException?.Message);
                return;
            }
            Assert.True(false, "Json Deserialize MUST throw here (security issue!)");
        }

        [Test]
        public void CanCreateSerializationAfterBinding()
        {
            var kernel = new StandardKernel(new MicrodotModule());
            var serializationBinder = kernel.Get<IMicrodotTypePolicySerializationBinder>();
            Assert.AreEqual(typeof(MicrodotTypePolicySerializationBinder), serializationBinder.GetType());
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
