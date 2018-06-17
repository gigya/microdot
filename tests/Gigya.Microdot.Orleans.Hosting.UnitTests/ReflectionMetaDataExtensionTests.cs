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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.Testing.Shared.Helpers;
using NUnit.Framework;
using Gigya.ServiceContract.Attributes;
using NSubstitute;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class ReflectionMetaDataExtensionTests
    {
        private ILog _logMocked;

        [SetUp]
        public void OneTimeSetup()
        {
            _logMocked = Substitute.For<ILog>();
        }


        [Test]
        [TestCase(nameof(PersonMockData.Sensitive), Sensitivity.Sensitive)]
        [TestCase(nameof(PersonMockData.Cryptic), Sensitivity.Secretive)]
        [TestCase(nameof(PersonMockData.Name), Sensitivity.NonSensitive)]
        [TestCase(nameof(PersonMockData.ID), null)]
        public void ExtracPropertiesSensitivity_ExtractSensitivity_ShouldBeEquivilent(string actualValue, Sensitivity? expected)
        {
            var expectedSensitiveProperty = typeof(PersonMockData).GetProperty(actualValue);

            PropertiesMetadataPropertiesCache.ExtractSensitivity(expectedSensitiveProperty).ShouldBe(expected);


        }

        [Test]
        [TestCase(nameof(PersonMockData.FieldNonSensitive), Sensitivity.NonSensitive)]
        [TestCase(nameof(PersonMockData.FieldSensitive), Sensitivity.Sensitive)]
        [TestCase(nameof(PersonMockData.FieldCryptic), Sensitivity.Secretive)]
        public void ExtracFieldsSensitivity_ExtractSensitivity_ShouldBeEquivilent(string actualValue, Sensitivity? expected)
        {
            var field = typeof(PersonMockData).GetField(actualValue);

            PropertiesMetadataPropertiesCache.ExtractSensitivity(field).ShouldBe(expected);
        }


        [Test]
        public void ExtracMembersValues_ExtractDataFromObject_ShouldBeEquivilent()
        {
            const int numberOfPrivatePropertiesAndFields = 8;

            var mock = new PersonMockData();
            var reflectionMetadataInfos = PropertiesMetadataPropertiesCache.ExtracPropertiesMetadata(mock, mock.GetType()).ToDictionary(x => x.Name);



            reflectionMetadataInfos[nameof(PersonMockData.FieldNonSensitive)].ValueExtractor(mock).ShouldBe(mock.FieldNonSensitive);
            reflectionMetadataInfos[nameof(PersonMockData.FieldSensitive)].ValueExtractor(mock).ShouldBe(mock.FieldSensitive);
            reflectionMetadataInfos[nameof(PersonMockData.FieldCryptic)].ValueExtractor(mock).ShouldBe(mock.FieldCryptic);

            reflectionMetadataInfos[nameof(PersonMockData.ID)].ValueExtractor(mock).ShouldBe(mock.ID);
            reflectionMetadataInfos[nameof(PersonMockData.Name)].ValueExtractor(mock).ShouldBe(mock.Name);
            reflectionMetadataInfos[nameof(PersonMockData.IsMale)].ValueExtractor(mock).ShouldBe(mock.IsMale);
            reflectionMetadataInfos[nameof(PersonMockData.Sensitive)].ValueExtractor(mock).ShouldBe(mock.Sensitive);
            reflectionMetadataInfos[nameof(PersonMockData.Cryptic)].ValueExtractor(mock).ShouldBe(mock.Cryptic);
        }



        [Test]
        public void ExtracPropertiesAndFieldsValues_ExtractDataFromObject_ShouldBeEquivilent()
        {
            const int numberOfPrivatePropertiesAndFields = 8;

            var mock = new PersonMockData();
            var reflectionMetadataInfos = PropertiesMetadataPropertiesCache.ExtracPropertiesMetadata(mock, mock.GetType()).ToDictionary(x => x.Name);
            var numberProperties = CalculateFieldsAndProperties(mock);

            reflectionMetadataInfos.Count.ShouldBe(numberProperties);


            int count = 0;
            foreach (var member in DissectPropertyInfoMetadata.GetMembers(mock))
            {
                var result = reflectionMetadataInfos[member.Name].ValueExtractor(mock);

                member.Value.ShouldBe(result, $"Propery name {member.Name} doesn't exists.");
                count++;
            }

            count.ShouldBe(numberProperties);
            count.ShouldBe(numberOfPrivatePropertiesAndFields);
            reflectionMetadataInfos.Count.ShouldBe(numberProperties);
        }




        [Test]
        public void ExtracPropertiesValues_ExtractSensitiveAndCryptic_ShouldBeEquivilent()
        {
            var mock = new PersonMockData();
            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var numberProperties = CalculateFieldsAndProperties(mock);


            var metadataCacheParams = cache.ParseIntoParams(mock);
            var dissectedParams = DissectPropertyInfoMetadata.GetMemberWithSensitivity(mock).ToDictionary(x => x.Name);

            int count = AssertBetweenCacheParamAndDissectParams(metadataCacheParams, dissectedParams);

            metadataCacheParams.Count().ShouldBe(dissectedParams.Count);
            count.ShouldBe(dissectedParams.Count);

        }


        [Test]
        public void ExtracPropertiesValues_ExtractSensitiveAndCrypticWithInheritenceAndException_ShouldBeEquivilent()
        {
            var mock = new TeacherWithExceptionMock();
            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var numberProperties = CalculateFieldsAndProperties(mock);


            var dissectedParams = DissectPropertyInfoMetadata.GetMemberWithSensitivity(mock).ToDictionary(x => x.Name);


            var parseParams = cache.ParseIntoParams(mock);
            int count = AssertBetweenCacheParamAndDissectParams(parseParams, dissectedParams);

            parseParams.Count().ShouldBe(dissectedParams.Count - 1);
            count.ShouldBe(dissectedParams.Count - 1);

            _logMocked.Received().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());

        }


        [Test]
        public void ExtracPropertiesValues_SendTwoPeople_ShouldBeEquivilent()
        {
            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var person = new PersonMockData();
            var teacher = new TeacherWithExceptionMock();


            var teacherArguments = cache.ParseIntoParams(teacher);
            var personArguments = cache.ParseIntoParams(person);

            var personDissect = DissectPropertyInfoMetadata.GetMemberWithSensitivity(person).ToDictionary(x => x.Name);


            var count = AssertBetweenCacheParamAndDissectParams(personArguments, personDissect);

            _logMocked.DidNotReceive().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
            count.ShouldBe(CalculateFieldsAndProperties(person));


            var teacherDissect = DissectPropertyInfoMetadata.GetMemberWithSensitivity(teacher).ToDictionary(x => x.Name);

            count = AssertBetweenCacheParamAndDissectParams(teacherArguments, teacherDissect);
            count.ShouldBe(teacherDissect.Count - 1);
            _logMocked.Received().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
        }

        [Test]
        public void LoadTest()
        {
            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var people = GeneratePeople(10000).ToList();
            var numOfProperties = CalculateFieldsAndProperties(new PersonMockData());
            var stopWatch = new Stopwatch();

            stopWatch.Start();

            foreach (var person in people)
            {
                var tmpParams = cache.ParseIntoParams(person);
                tmpParams.Count().ShouldBe(numOfProperties);
            }
            stopWatch.Stop();
        }

        private static int AssertBetweenCacheParamAndDissectParams(IEnumerable<MetadataCacheParam> @params, Dictionary<string, (object Value, MemberTypes MemberType, string Name, Sensitivity? Sensitivity, bool WithException, MemberInfo Member)> actual)
        {
            int count = 0;
            foreach (var param in @params)
            {
                var metadata = actual[param.Name];

                param.Sensitivity.ShouldBe(metadata.Sensitivity);
                param.Value.ShouldBe(metadata.Value);

                count++;
            }

            return count;
        }

        private int CalculateFieldsAndProperties<TInstance>(TInstance instance) where TInstance : class
        {
            var numOfProperties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Length + typeof(PersonMockData).GetFields(BindingFlags.Public | BindingFlags.Instance).Length;
            return numOfProperties;
        }

        private IEnumerable<PersonMockData> GeneratePeople(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                yield return new PersonMockData { ID = i, Name = "Name", Cryptic = true };
            }


        }

        #region MockData
        private class PersonMockData
        {
            //PRIVATE - Should be Ignored

            [NonSensitive]
            private string PrivateFieldNonSensitive = "PrivateFieldName";


            [Sensitive(Secretive = false)]

            private string PrivateFieldSensitive = "PrivateFieldSensitive";

            [Sensitive(Secretive = true)]

            private string PrivateFieldCryptic = "PrivateFieldCryptic";


            //--------------------------------------------------------------------------------------------------------------------------------------
            //--------------------------------------------------------------------------------------------------------------------------------------

            //PUBLIC

            [NonSensitive]
            public string FieldNonSensitive = "FieldName";


            [Sensitive(Secretive = false)]

            public string FieldSensitive = "FieldSensitive";

            [Sensitive(Secretive = true)]

            public string FieldCryptic = "FieldCryptic";


            //--------------------------------------------------------------------------------------------------------------------------------------
            //--------------------------------------------------------------------------------------------------------------------------------------

            //PUBLIC Properties

            public int ID { get; set; } = 10;

            [NonSensitive]
            public string Name { get; set; } = "Mocky";

            public bool IsMale { get; set; } = false;

            [Sensitive(Secretive = false)]

            public bool Sensitive { get; set; } = true;

            [Sensitive(Secretive = true)]

            public bool Cryptic { get; set; } = true;


            //--------------------------------------------------------------------------------------------------------------------------------------
            //--------------------------------------------------------------------------------------------------------------------------------------

            //PRIVATE Properties - Should be Ignored

            private int PrivateID { get; set; } = 10;

            [NonSensitive]
            private string PrivateName { get; set; } = "Mocky";

            private bool PrivateIsMale { get; set; } = false;

            [Sensitive(Secretive = false)]

            private bool PrivateSensitive { get; set; } = true;

            [Sensitive(Secretive = true)]

            private bool PrivateCryptic { get; set; } = true;

            //--------------------------------------------------------------------------------------------------------------------------------------
            //--------------------------------------------------------------------------------------------------------------------------------------

            //PUBLIC Methods - Should be Ignored

            public void ShouldBeIgnoreMetho()
            {

            }

        }

        private class TeacherWithExceptionMock : PersonMockData
        {
            public string GetError => throw new Exception("ddsfsdfasd");

            [NonSensitive]
            public int Years { get; set; }
        }
        #endregion
    }
}

