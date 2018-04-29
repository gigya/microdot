﻿#region Copyright
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.HttpService;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.Testing.Service;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.Testing.Shared.Helpers;
using Gigya.ServiceContract.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ninject;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    public class Wrapper
    {
        public int INT { get; set; }
        public string STR { get; set; }
    }

    public class JObjectWrapper
    {

        public JObject JObject { get; private set; }

        public JObjectWrapper(JObject jObject)
        {
            this.JObject = jObject;
        }
    }



    [TestFixture]
    public class CalculatorServiceTests
    {
        private ServiceTester<CalculatorServiceHost> Tester { get; set; }
        private ICalculatorService Service { get; set; }
        private ICalculatorService ServiceWithCaching { get; set; }


        [OneTimeSetUp]
        public void SetUp()
        {
            try
            {

                Tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<CalculatorServiceHost>(writeLogToFile: true,serviceDrainTime:TimeSpan.MaxValue);
                Service = Tester.GetServiceProxy<ICalculatorService>();
                ServiceWithCaching = Tester.GetServiceProxyWithCaching<ICalculatorService>();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }


        [OneTimeTearDown]
        public void TearDown()
        {
            Tester.Dispose();
        }


        [Test]
        public async Task Add_NumbersVia_JObjectWrapper()
        {
            JObjectWrapper jObjectW = new JObjectWrapper(new JObject());
            jObjectW.JObject["a"] = 5;
            jObjectW.JObject["b"] = 3;

            jObjectW = await Service.Add(jObjectW);

            var actual = jObjectW.JObject["c"].Value<int>();
            actual.ShouldBe(8);
        }

        [Test]
        public async Task CallingMethodWithOptionalParametersWork()
        {
            var arguments = new object[] { new JObject() };

            var res = await CallService(arguments);
            res.Item1.ShouldBe(5);
            res.Item2.ShouldBe("test");
            res.Item3.ShouldBe(null);
        }

        private async Task<Tuple<int, string, JObject>> CallService(object[] arguments)
        {
            var request = new HttpServiceRequest(typeof(ICalculatorService).GetMethod(nameof(ICalculatorService.AddWithOptions)), arguments);
            request.Target.ParameterTypes = request.Target.ParameterTypes.Take(arguments.Length).ToArray();
            var proxy = Tester.GetServiceProxyProvider("CalculatorService");
            var res = (Tuple<int, string, JObject>)await proxy.Invoke(request, typeof(Tuple<int, string, JObject>));
            return res;
        }

        [Test]
        public async Task Add_NumbersVia_JObject()
        {
            JObject jObject = new JObject();
            jObject["a"] = 5;
            jObject["b"] = 3;

            jObject = await Service.Add(jObject);

            var actual = jObject["c"].Value<int>();
            actual.ShouldBe(8);
        }

        [Test]
        public async Task Add_SmallNumbers_ReturnsCorrectResult()
        {
            var actual = await Service.Add(5, 3);

            actual.ShouldBe(8);
        }

        [Test]
        public async Task Add_LargeNumbers_ReturnsCorrectResult()
        {
            var actual = await Service.Add(1000, 30000);

            actual.ShouldBe(31000);
        }

        [Test]
        public async Task Add_NegativeNumbers_ReturnsCorrectResult()
        {
            var actual = await Service.Add(-8, -3);

            actual.ShouldBe(-11);
        }

        [Test]
        public async Task ToUniversalTimeAsWeakRequest_FixedDates_ReturnsCorrectResult()
        {
            const string localDateString = "2016-07-31T10:00:00+03:00";
            var localDateTime = DateTime.Parse(localDateString);
            var localDateTimeOffset = DateTimeOffset.Parse(localDateString);
            var ukernel = new TestingKernel<ConsoleLog>();

            var providerFactory = ukernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory("CalculatorService");
            var dict = new Dictionary<string, object>
            {
                {"localDateTime", localDateTime},
                {"localDateTimeOffset", localDateTimeOffset}
            };
            serviceProxy.DefaultPort = 6555;

            var res = await serviceProxy.Invoke(new HttpServiceRequest("ToUniversalTime", typeof(ICalculatorService).FullName, dict), typeof(JObject));
            var json = (JToken)res;
            DateTimeOffset.Parse(json["Item1"].Value<string>()).ShouldBe(DateTime.Parse(localDateString).ToUniversalTime());
            DateTimeOffset.Parse(json["Item2"].Value<string>()).ShouldBe(localDateTimeOffset.DateTime);
        }

        [Test]
        public async Task CallWeakRequestWith_ComplexObject_ParamAndNoReturnType()
        {
            var ukernel = new TestingKernel<ConsoleLog>();

            var providerFactory = ukernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory("CalculatorService");
            var wrapper = new Wrapper { INT = 100, STR = "100" };
            var dict = new Dictionary<string, object> { { "wrapper", JsonConvert.SerializeObject(wrapper) } };
            serviceProxy.DefaultPort = 6555;

            var res = await serviceProxy.Invoke(new HttpServiceRequest("DoComplex", typeof(ICalculatorService).FullName, dict), typeof(JObject));

            ((JToken)res).ToObject<Wrapper>().INT.ShouldBe(wrapper.INT);
            ((JToken)res).ToObject<Wrapper>().STR.ShouldBe(wrapper.STR);
        }


        [Test]
        public async Task CallWeakRequestWith_Int_ParamAndNoReturnType()
        {
            var ukernel = new TestingKernel<ConsoleLog>();

            var providerFactory = ukernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory("CalculatorService");
            var dict = new Dictionary<string, object> { { "a", "5" } };
            serviceProxy.DefaultPort = 6555;

            var res = await serviceProxy.Invoke(new HttpServiceRequest("DoInt", typeof(ICalculatorService).FullName, dict), typeof(JObject));
            ((JToken)res).Value<int>().ShouldBe(5);
        }


        [Test]
        public async Task CallWeakRequestWith_NoParamsAndNoReturnType()
        {
            var ukernel = new TestingKernel<ConsoleLog>();

            var providerFactory = ukernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory("CalculatorService");
            var dict = new Dictionary<string, object>();
            serviceProxy.DefaultPort = 6555;

            var res = await serviceProxy.Invoke(new HttpServiceRequest("Do", typeof(ICalculatorService).FullName, dict), typeof(JObject));
            var json = (JToken)res;
            json.ShouldBe(null);
        }

        [Test]
        public async Task CallWeakRequestWith_NoParamsAndNoReturnTypeAndNoType()
        {
            var ukernel = new TestingKernel<ConsoleLog>();

            var providerFactory = ukernel.Get<Func<string, ServiceProxyProvider>>();
            var serviceProxy = providerFactory("CalculatorService");
            var dict = new Dictionary<string, object>();
            serviceProxy.DefaultPort = 6555;

            var res = await serviceProxy.Invoke(new HttpServiceRequest("Do", dict), typeof(JObject));
            var json = (JToken)res;
            json.ShouldBe(null);
        }

        [Test]
        public async Task ToUniversalTime_FixedDates_ReturnsCorrectResult()
        {
            const string localDateString = "2016-07-31T10:00:00+03:00";
            var localDateTime = DateTime.Parse(localDateString);
            var localDateTimeOffset = DateTimeOffset.Parse(localDateString);

            var actual = await Service.ToUniversalTime(localDateTime, localDateTimeOffset);

            actual.Item1.ShouldBe(DateTime.Parse(localDateString).ToUniversalTime());
            actual.Item1.Kind.ShouldBe(DateTimeKind.Utc);

            actual.Item2.ShouldBe(DateTimeOffset.Parse(localDateString).ToUniversalTime());
            actual.Item2.Offset.ShouldBe(TimeSpan.Zero);
        }

        [Test]
        public async Task ValueShouldBeCached()
        {
            var firstValue = await ServiceWithCaching.GetNextNum();
            await Task.Delay(1);
            var secondValue = await ServiceWithCaching.GetNextNum();
            //Items shouldBe come from the Cache
            secondValue.ShouldBe(firstValue);
        }

        [Test]
        public async Task ValueShouldBeRevoked()
        {
            string id = $"Test-{DateTime.UtcNow}";
            var firstValue = await ServiceWithCaching.GetVersion(id);
            await Task.Delay(1);
            var secondValue = await ServiceWithCaching.GetVersion(id);

            //Items shouldBe come from the Cache
            secondValue.ShouldBe(firstValue);

            await AssemblyInitialize.ResolutionRoot.Get<ICacheRevoker>().Revoke(id);

            //Items shouldBe remove from Cache
            await Task.Delay(200);
            var threadValue = await ServiceWithCaching.GetVersion(id);

            threadValue.ShouldNotBe(secondValue);
        }

        [Test]
        public async Task LogTest()
        {
            var logMessage = $"log-{Guid.NewGuid()}";
            await Service.LogData(logMessage);
            await Task.Delay(100);
            File.ReadAllText("TestLog.txt").ShouldContain(logMessage);
        }

        [Test]
        public async Task ShouldPublishEventWithCallParametersDefault()
        {
            var sensitive = "sensitive test";
            var nonsensitive = "nonsensitive Test";
            var notExists = "notExists Test";
            var @default = "default Test";

            await Service.LogPram(sensitive, nonsensitive, notExists, @default);
            (await Service.IsLogParamSucceeded(new List<string> { @default, sensitive }, new List<string> { nonsensitive }, new List<string> { notExists })).ShouldBeTrue();
        }

        [Test]
        public async Task ShouldPublishEventWithCallParametersMethodNonsensitive()
        {
            var sensitive = "sensitive test";
            var nonsensitive = "nonsensitive Test";
            var notExists = "notExists Test";
            var @default = "default Test";

            await Service.LogPram2(sensitive, nonsensitive, notExists, @default);
            (await Service.IsLogParamSucceeded(new List<string> { sensitive }, new List<string> { nonsensitive, @default }, new List<string> { notExists })).ShouldBeTrue();
        }


        [Test]
        public async Task SendComplexRequest()
        {
            var person = new Person();

            await Service.CreatePerson(person);
            (await Service.IsLogParamSucceeded(
                sensitives: new List<string> { person.ID.ToString(), person.Gender },
                NoneSensitives: new List<string> { person.Name },
                NotExists: new List<string> { person.Password })).ShouldBeTrue();
        }

        [Test]
        public async Task SendComplexWithInheritenceRequest()
        {
            var teacher = new Teacher();

            await Service.CreatePerson(teacher);
            (await Service.IsLogParamSucceeded(
                sensitives: new List<string> { teacher.ID.ToString(), teacher.Gender },
                NoneSensitives: new List<string> { teacher.Name, teacher.School },
                NotExists: new List<string> { teacher.Password })).ShouldBeTrue();
        }



        [Test]
        public async Task CreateDynamicallyMockPerson()
        {
            var person = new Person();

            await Service.CreatePerson(person);
            (await Service.ValidatePersonLogFields(person)).ShouldBeTrue();
        }

        [Test]
        public async Task CreateDynamicallyWithInheritenceMock()
        {
            var person = new Teacher();

            await Service.CreatePerson(person);
            (await Service.ValidatePersonLogFields(person)).ShouldBeTrue();

        }

        [Test]
        public async Task LogGrainId()
        {
            await Service.LogGrainId();
        }

        [Test]
        public async Task OrleansSerialization_MyServiceException_IsEquivalent()
        {
            var myServiceException = GetMyException();

            var actual = await Should.ThrowAsync<MyServiceException>(() => Service.ThrowExceptionAndValidate(myServiceException));
            AssertExceptionsAreEqual(myServiceException, actual);

        }

        [Test]
        public async Task OrleansSerialization_InnerMyServiceException_IsEquivalent()
        {
            var myServiceException = GetMyException();
            var expected = new Exception("Intermediate exception", myServiceException).ThrowAndCatch();

            var actual = await Should.ThrowAsync<RemoteServiceException>(() => Service.ThrowExceptionAndValidate(new Exception("Intermediate exception", myServiceException)));

            AssertExceptionsAreEqual(expected, actual.InnerException);
        }


        [Test]
        public async Task OrleansSerialization_CustomerFacingException_IsEquivalent()
        {
            var expected = new RequestException("Test", 10000).ThrowAndCatch();
            var actual = await Should.ThrowAsync<RequestException>(() => Service.ThrowExceptionAndValidate(expected));

            AssertExceptionsAreEqual(expected, actual);
            expected.ErrorCode.ShouldBe(10000);
        }

        [Test]
        public async Task OrleansSerialization_HttpRequestException_IsEquivalent()
        {
            const string message = "HTTP request exception";
            var actual = await Should.ThrowAsync<EnvironmentException>(() => Service.ThrowHttpRequestException(message), $"[HttpRequestException] {message}");
            actual.UnencryptedTags.ShouldHaveSingleItem().Key.ShouldBe("originalStackTrace");

        }

        private void AssertExceptionsAreEqual(Exception expected, Exception actual)
        {
            Assert.NotNull(actual);
            Assert.AreEqual(expected.GetType(), actual.GetType());
            Assert.AreEqual(expected.Message, actual.Message);

            if (expected is SerializableException)
            {
                var typedExpected = expected as SerializableException;
                var typedActual = actual as SerializableException;
                CollectionAssert.AreEqual(typedExpected.EncryptedTags, typedActual.EncryptedTags);
                CollectionAssert.AreEqual(typedExpected.UnencryptedTags, typedActual.UnencryptedTags);
                Assert.AreEqual(typedActual.ExtendedProperties.Count, 0);
            }

            if (expected is MyServiceException myServiceException)
                Assert.AreEqual(myServiceException.Entity, ((MyServiceException)actual).Entity);

            if (expected.InnerException != null)
                AssertExceptionsAreEqual(expected.InnerException, actual.InnerException);
        }

        private MyServiceException GetMyException()
        {
            return new MyServiceException(
                "My message",
                new BusinessEntity { Name = "name", Number = 5 },
                unencrypted: new Tags { { "t1", "v1" } }).ThrowAndCatch();
        }

        #region MockData

        #region Seriliazation Tests
        [Serializable]
        private class MyServiceException : RequestException
        {
            public IBusinessEntity Entity { get; private set; }

            public MyServiceException(string message, IBusinessEntity entity, Tags encrypted = null, Tags unencrypted = null)
                : base(message, null, encrypted, unencrypted)
            {
                Entity = entity;
            }

            public MyServiceException(string message, Exception innerException) : base(message, innerException) { }
            public MyServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }


        private interface IBusinessEntity
        {
            string Name { get; set; }
            int Number { get; set; }
        }

        [Serializable]
        private class BusinessEntity : IBusinessEntity
        {
            public string Name { get; set; }
            public int Number { get; set; }

            public override bool Equals(object obj)
            {
                BusinessEntity other = (BusinessEntity)obj;
                return Name == other.Name && Number == other.Number;
            }

            public override int GetHashCode() { unchecked { return 0; } }
        }
        #endregion

        #region For LofFieldAttrobute
        public class Person
        {
            public int ID { get; set; } = 100;

            [NonSensitive]
            public string Name { get; set; } = "Eli";

            [Sensitive(Secretive = false)]
            public string Gender { get; set; } = "Man";

            [Sensitive(Secretive = true)]
            public string Password { get; set; } = "password";

            public InnerCarMockClass InnerCarMockClass { get; set; } = new InnerCarMockClass();
        }

        public class InnerCarMockClass
        {
            [NonSensitive] public int Year { get; set; } = 100;

            [NonSensitive] public string LisencePlates { get; set; } = "11 -222-33";
        }

        [Serializable]
        public class Teacher : Person
        {
            [NonSensitive]
            public string School { get; set; } = "Busmat";
        }
        #endregion

        #endregion
    }
}
