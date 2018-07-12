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
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Testing.Shared;
using Gigya.ServiceContract.Attributes;
using Ninject;
using NSubstitute;
using NUnit.Framework;

namespace Gigya.Common.Application.UnitTests.Validation
{
    [TestFixture]

    public class SensitivityAttributesValidatorTests
    {
        private const int Port = 0;

        private IValidator _serviceValidator;
        private IServiceInterfaceMapper _serviceInterfaceMapper;
        private Type[] _typesToValidate;

        [SetUp]
        public void Setup()
        {
            _serviceInterfaceMapper = Substitute.For<IServiceInterfaceMapper>();
            _serviceInterfaceMapper.ServiceInterfaceTypes.Returns(_ => _typesToValidate);
            var unitTesting = new TestingKernel<ConsoleLog>(kernel => kernel.Rebind<IServiceInterfaceMapper>().ToConstant(_serviceInterfaceMapper));
            _serviceValidator = unitTesting.Get<SensitivityAttributesValidator>();
        }

        [TestCase(typeof(TwoAttributeOnTheSameMethod))]
        [TestCase(typeof(TwoAttributeOnTheeSameParameter))]
        [TestCase(typeof(IInvalid_WithoutLogFieldAndWithSensitivityOnProperty))]
        [TestCase(typeof(IInvalid_WithoutLogFieldAndWithinNestedSensitivity))]
        [TestCase(typeof(IInvalid_WithLogFieldAndWithSensitivityOnField))]

        public void ValidationShouldFail(Type typeToValidate)
        {
            _typesToValidate = new[] { typeToValidate };
            Assert.Throws<ProgrammaticException>(_serviceValidator.Validate);
        }

        //[TestCase(typeof(IValidMock))]
        [TestCase(typeof(IComplexParameterValidation))]

        public void ValidationShouldSucceed(Type typeToValidate)
        {
            _typesToValidate = new[] { typeToValidate };
            _serviceValidator.Validate();
        }


        [HttpService(Port, Name = "This service contains methods with valid parameter types")]
        private interface TwoAttributeOnTheSameMethod
        {
            [Sensitive]
            [NonSensitive]
            Task NotValid();
        }

        [HttpService(Port, Name = "This service contains methods with valid parameter types")]
        private interface TwoAttributeOnTheeSameParameter
        {
            Task NotValid([Sensitive] [NonSensitive]string test);
        }


        #region IInvalid_WithoutLogFieldAndWithSensitivityOnProperty

        [HttpService(Port, Name = "This service contains methods with invalid parameter types")]
        private interface IInvalid_WithoutLogFieldAndWithSensitivityOnProperty
        {
            Task NotValid(SchoolWithhAttributeWithoutNested schoolWithhAttributeWithoutNested);
        }


        public class SchoolWithhAttributeWithoutNested
        {
            [Sensitive]
            public string Address { get; set; } = "Bad";
            public string FieldAddress = "Bad";
        }

        #endregion

        #region IInvalid_WithoutLogFieldAndWithinNestedSensitivity

        [HttpService(Port, Name = "This service contains methods with invalid parameter types")]
        private interface IInvalid_WithoutLogFieldAndWithinNestedSensitivity
        {
            Task NotValid(SchoolWithhLevel2Attribute test);
        }


        public class SchoolWithhLevel2Attribute
        {
            public class StudentWithFieldAttribute
            {
                [Sensitive]
                public string StudentName = "Maria";

                public string FamilyName { get; set; } = "Bad";

                public int Age { get; set; } = 20;
            }

            public string FieldName = "Maria";
            public string SchoolName = "Maria";

            public string Address { get; set; } = "Bad";
            public string FieldAddress { get; set; } = "Bad";

            public StudentWithFieldAttribute Student { get; set; } = new StudentWithFieldAttribute();


        }

        #endregion

        #region IInvalid_WithLogFieldAndWithSensitivityOnField

        [HttpService(Port, Name = "This service contains methods with invalid parameter types")]
        private interface IInvalid_WithLogFieldAndWithSensitivityOnField
        {
            Task NotValid(SchoolWithhLevel2Attribute test);
        }

        #endregion

        #region IComplexParameterValidation
        [HttpService(Port, Name = "This service contains valid methods.")]
        public interface IComplexParameterValidation
        {

            Task CreateSchoolWithLogField(SchoolWithoutAttributes schoolWithoutAttributes);
            Task CreateSchoolWithLogField(SchooWithNestedClassWithoutAttributes param1);
            Task CreateSchoolWithLogField(SchooWithNestedClassWithoutAttributes param1, SchooWithNestedClassWithoutAttributes schoolValidator2, string test);


            Task CreateSchoolWithoutLogField([LogFields]FactoryWithNestedClassWithAttribute factory);
            Task CreateSchoolWithoutLogField([LogFields]SchooWithNestedClassWithoutAttributes param1, SchooWithNestedClassWithoutAttributes schoolValidator2);
            Task CreateSchoolWithoutLogField([LogFields]SchooWithNestedClassWithoutAttributes param1, [LogFields]FactoryWithNestedClassWithAttribute factory, string test);

        }

        public class SchooWithNestedClassWithoutAttributes
        {
            public class StudentWithoutAttribute
            {
                public string Name { get; set; } = "Maria";

                public string FamilyName { get; set; } = "Bad";

                public int Age { get; set; } = 20;
            }


            public string FieldName = "Maria";
            public string SchoolName = "Maria";

            public string Address { get; set; } = "Bad";
            public string FieldAddress { get; set; } = "Bad";

            public StudentWithoutAttribute Student { get; set; } = new StudentWithoutAttribute();
        }


        public class FactoryWithNestedClassWithAttribute
        {
            public class WorkerWithoutAttribute
            {
                //[NonSensitive]

                public string InnerName { get; set; } = "Maria";

                public string InnerFamilyName { get; set; } = "Bad";

                public int InnerAge { get; set; } = 20;
            }




            [NonSensitive]
            public string FieldName = "Maria";
            [NonSensitive]

            public string SchoolName = "Maria";


            public WorkerWithoutAttribute Student { get; set; } = new WorkerWithoutAttribute();


            [NonSensitive]

            public string Address { get; set; } = "Bad";
            [NonSensitive]

            public string FieldAddress { get; set; } = "Bad";

        }

        public class SchoolWithoutAttributes
        {
            public string FieldName { get; set; } = "Maria";
            public string SchoolName { get; set; } = "Maria";
        }
        #endregion

        private interface IValidMock
        {
            Task valid([Sensitive] string test);
            Task valid2([NonSensitive] string test);
            [NonSensitive]
            Task valid3([NonSensitive] string test);
            [Sensitive]
            Task valid4([NonSensitive] string test);

            [NonSensitive]
            Task valid5([Sensitive] string test);
            [Sensitive]
            Task valid6([Sensitive] string test);
            Task valid7([Sensitive] string test, [NonSensitive] string test2);



        }
    }
}