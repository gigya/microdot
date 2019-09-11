#region Copyright 
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



using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Validators;
using Gigya.ServiceContract.Attributes;
using Newtonsoft.Json.Linq;
using NSubstitute;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class 
        LogFieldAttributeValidatorTest
    {
        private Type[] _typesToValidate;
        private IServiceInterfaceMapper _serviceInterfaceMapper;


        [SetUp]
        public void Setup()
        {
            _serviceInterfaceMapper = Substitute.For<IServiceInterfaceMapper>();
            _serviceInterfaceMapper.ServiceInterfaceTypes.Returns(_ => _typesToValidate);
        }


        [TestCase(typeof(ILogFieldOnStringMockData))]
        [TestCase(typeof(ILogFieldOnIntMockData))]
        [TestCase(typeof(ILogFieldOnGuiMockData))]
        [TestCase(typeof(ILogFieldOnIEnumerableMockData))]
        [TestCase(typeof(ILogFieldOnStructMockData))]
        [TestCase(typeof(ILogFieldOnDateTimeMockData))]
        [TestCase(typeof(ILogFieldOnDateTimeOffsetMockData))]
        [TestCase(typeof(ILogFieldOnTypeMockData))]
        [TestCase(typeof(ILogFieldOnJTokenMockData))]
        public void ValidationShouldFail(Type typeToValidate)
        {
            _typesToValidate = new[] { typeToValidate };
            var logFieldAttributeValidator = new LogFieldAttributeValidator(_serviceInterfaceMapper);

            Assert.Throws<ProgrammaticException>(logFieldAttributeValidator.Validate);
        }


        [TestCase(typeof(IPersonValidRequest))]
        public void ValidationShouldPass(Type typeToValidate)
        {
            _typesToValidate = new[] { typeToValidate };
            var logFieldAttributeValidator = new LogFieldAttributeValidator(_serviceInterfaceMapper);

            logFieldAttributeValidator.Validate();
        }



        #region Valid MockData
        // ReSharper disable once ClassNeverInstantiated.Local
        private class LocalPerson
        {
            public string Name { get; set; }
            public Guid Id { get; set; }
        }

        private interface IPersonValidRequest
        {
            Task AddPerson(LocalPerson localPerson);
            Task AddPerson();
            Task AddPersonWithLogFieldAttribute([LogFields]LocalPerson localPerson);
            Task AddPerson(LocalPerson localPerson1, [LogFields] LocalPerson localPerson2);
            Task AddPerson2([LogFields] LocalPerson localPerson1, [LogFields] LocalPerson localPerson2);
        }
        #endregion

        #region WrongData

        private struct MyStruct
        { }

        private interface ILogFieldOnStringMockData
        {
            void AddPerson([LogFields] string name);
        }

        private interface ILogFieldOnIntMockData
        {
            void AddPerson([LogFields] int id);
        }

        private interface ILogFieldOnGuiMockData
        {
            void AddPerson([LogFields] Guid id);
        }

        private interface ILogFieldOnIEnumerableMockData
        {
            void AddPerson([LogFields] IEnumerable<string> id);
        }

        private interface ILogFieldOnStructMockData
        {
            void AddPerson([LogFields] MyStruct d);
        }

        private interface ILogFieldOnDateTimeMockData
        {
            void AddPerson([LogFields] DateTime dateTime);
        }
        private interface ILogFieldOnDateTimeOffsetMockData
        {
            void AddPerson([LogFields] DateTimeOffset dateTimeOffset);
        }
        private interface ILogFieldOnTypeMockData
        {
            void AddPerson([LogFields] Type type);
        }

        private interface ILogFieldOnJTokenMockData
        {
            void AddPerson([LogFields] JToken token);
        }
        #endregion
    }
}
