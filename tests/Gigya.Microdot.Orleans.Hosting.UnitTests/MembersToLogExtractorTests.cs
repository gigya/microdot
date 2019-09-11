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
using System.Linq;
using System.Reflection;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.Testing.Shared.Helpers;
using NUnit.Framework;
using Gigya.ServiceContract.Attributes;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class MembersToLogExtractorTests
    {
        private ILog _logMocked;
        private MembersToLogExtractor _extractor;

        [SetUp]
        public void OneTimeSetup()
        {
            _logMocked = Substitute.For<ILog>();
            _extractor = new MembersToLogExtractor(_logMocked);
        }


        [Test]
        [TestCase(nameof(PersonMockData.Sensitive), Sensitivity.Sensitive)]
        [TestCase(nameof(PersonMockData.Cryptic), Sensitivity.Secretive)]
        [TestCase(nameof(PersonMockData.Name), Sensitivity.NonSensitive)]
        [TestCase(nameof(PersonMockData.ID), null)]
        public void ExtractPropertiesSensitivity_ExtractSensitivity_ShouldBeEquivilent(string actualValue, Sensitivity? expected)
        {
            var expectedSensitiveProperty = typeof(PersonMockData).GetProperty(actualValue);

            _extractor.ExtractSensitivity(expectedSensitiveProperty).ShouldBe(expected);
        }

        [Test]
        [TestCase(nameof(PersonMockData.FieldNonSensitive), Sensitivity.NonSensitive)]
        [TestCase(nameof(PersonMockData.FieldSensitive), Sensitivity.Sensitive)]
        [TestCase(nameof(PersonMockData.FieldCryptic), Sensitivity.Secretive)]
        public void ExtractFieldsSensitivity_ExtractSensitivity_ShouldBeEquivilent(string actualValue, Sensitivity? expected)
        {
            var field = typeof(PersonMockData).GetField(actualValue);

            _extractor.ExtractSensitivity(field).ShouldBe(expected);
        }


        [Test]
        public void ExtractMembersValues_ExtractDataFromObject_ShouldBeEquivilent()
        {
            const int numberOfPrivatePropertiesAndFields = 9;

            var mock = new PersonMockData();
            var reflectionMetadataInfos = _extractor.ExtractMemberMetadata(mock.GetType()).ToDictionary(x => x.Name);

            reflectionMetadataInfos[nameof(PersonMockData.FieldNonSensitive)].ValueExtractor(mock).ShouldBe(mock.FieldNonSensitive);
            reflectionMetadataInfos[nameof(PersonMockData.FieldSensitive)].ValueExtractor(mock).ShouldBe(mock.FieldSensitive);
            reflectionMetadataInfos[nameof(PersonMockData.FieldCryptic)].ValueExtractor(mock).ShouldBe(mock.FieldCryptic);

            reflectionMetadataInfos[nameof(PersonMockData.ID)].ValueExtractor(mock).ShouldBe(mock.ID);
            reflectionMetadataInfos[nameof(PersonMockData.Name)].ValueExtractor(mock).ShouldBe(mock.Name);
            reflectionMetadataInfos[nameof(PersonMockData.IsMale)].ValueExtractor(mock).ShouldBe(mock.IsMale);
            reflectionMetadataInfos[nameof(PersonMockData.Sensitive)].ValueExtractor(mock).ShouldBe(mock.Sensitive);
            reflectionMetadataInfos[nameof(PersonMockData.Cryptic)].ValueExtractor(mock).ShouldBe(mock.Cryptic);
            reflectionMetadataInfos[nameof(PersonMockData.JObjectFieldNonSensitive)].ValueExtractor(mock).ShouldBe(mock.JObjectFieldNonSensitive);

            
            reflectionMetadataInfos.Count.ShouldBe(numberOfPrivatePropertiesAndFields);
        }

        [Test]
        public void ExtractPropertiesAndFieldsValues_ExtractDataFromObject_ShouldBeEquivilent()
        {
            const int numberOfPrivatePropertiesAndFields = 9;

            var mock = new PersonMockData();
            var reflectionMetadataInfos = _extractor.ExtractMemberMetadata(mock.GetType()).ToDictionary(x => x.Name);
            var dissectParams = DissectPropertyInfoMetadata.GetMembers(mock);
            var numberProperties = dissectParams.Count();


            int count = 0;
            foreach (var member in dissectParams)
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
        public void ExtractMembersToLog_ExtractSensitiveAndCryptic_ShouldBeEquivilent()
        {
            var mock = new PersonMockData();

            var metadataCacheParams = _extractor.ExtractMembersToLog(mock);
            var dissectedParams = DissectPropertyInfoMetadata.GetMemberWithSensitivity(mock).ToDictionary(x => x.Name);

            int count = AssertBetweenCacheParamAndDissectParams(metadataCacheParams, dissectedParams);

            metadataCacheParams.Count().ShouldBe(dissectedParams.Count);
            count.ShouldBe(dissectedParams.Count);

        }

        [Test]
        public void ExtractMembersToLog_ExtractSensitiveAndCrypticWithInheritenceAndException_ShouldBeEquivilent()
        {
            var mock = new TeacherWithExceptionMock();

            var dissectedParams = DissectPropertyInfoMetadata.GetMemberWithSensitivity(mock).ToDictionary(x => x.Name);

            var parseParams = _extractor.ExtractMembersToLog(mock);
            int count = AssertBetweenCacheParamAndDissectParams(parseParams, dissectedParams);

            parseParams.Count().ShouldBe(dissectedParams.Count - 1);
            count.ShouldBe(dissectedParams.Count - 1);

            _logMocked.Received().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
        }

        [Test]
        public void ExtractMembersToLog_SendTwoPeople_ShouldBeEquivilent()
        {            
            var person = new PersonMockData();
            var teacher = new TeacherWithExceptionMock();

            var teacherArguments = _extractor.ExtractMembersToLog(teacher);
            var personArguments = _extractor.ExtractMembersToLog(person);

            var personDissect = DissectPropertyInfoMetadata.GetMemberWithSensitivity(person).ToDictionary(x => x.Name);


            var count = AssertBetweenCacheParamAndDissectParams(personArguments, personDissect);

            _logMocked.DidNotReceive().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
            count.ShouldBe(personDissect.Count);


            var teacherDissect = DissectPropertyInfoMetadata.GetMemberWithSensitivity(teacher).ToDictionary(x => x.Name);

            count = AssertBetweenCacheParamAndDissectParams(teacherArguments, teacherDissect);
            count.ShouldBe(teacherDissect.Count - 1);
            _logMocked.Received().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
        }

        [Test]
        public void ExtractMembersToLog_TypeWithGeneric_MembersExtractedCorrectly()
        {
            var genericMockData = new GenericMockData<MockData>(new MockData());

            var genericArguments = _extractor.ExtractMembersToLog(genericMockData);

            _logMocked.DidNotReceive().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
            genericArguments.Count().ShouldBe(3);
            genericArguments.ShouldContain(x => x.Name == "FieldNonSensitive" && x.Value.ToString() == "FieldNonSensitiveValue" && x.Sensitivity == Sensitivity.NonSensitive);
            genericArguments.ShouldContain(x => x.Name == "GenericType_GenericPropertySensitive" && x.Value.ToString() == "GenericPropertySensitiveValue" && x.Sensitivity == Sensitivity.Sensitive);
            genericArguments.ShouldContain(x => x.Name == "GenericType_GenericFieldSecretive" && x.Value.ToString() == "GenericFieldSecretiveValue" && x.Sensitivity == Sensitivity.Secretive);
        }

        [Test]
        public void ExtractMembersToLog_GenericWithInheritance_MembersExtractedCorrectly()
        {
            var genericWithInheritance = new GenericWithInheritance<DerivedMockData>(new DerivedMockData());

            var genericWithInheritanceArguments = _extractor.ExtractMembersToLog(genericWithInheritance);

            _logMocked.DidNotReceive().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
            genericWithInheritanceArguments.Count().ShouldBe(4);
            genericWithInheritanceArguments.ShouldContain(x => x.Name == "GenericType_DerivedPropertySensitive" && x.Value.ToString() == "DerivedPropertySensitiveValue" && x.Sensitivity == Sensitivity.Sensitive);
            genericWithInheritanceArguments.ShouldContain(x => x.Name == "GenericType_GenericPropertySensitive" && x.Value.ToString() == "GenericPropertySensitiveValue" && x.Sensitivity == Sensitivity.Sensitive);
            genericWithInheritanceArguments.ShouldContain(x => x.Name == "GenericType_GenericFieldSecretive" && x.Value.ToString() == "GenericFieldSecretiveValue" && x.Sensitivity == Sensitivity.Secretive);
            genericWithInheritanceArguments.ShouldContain(x => x.Name == "GenericWithInheritancePropertySensitive" && x.Value.ToString() == "GenericWithInheritancePropertySensitiveValue" && x.Sensitivity == Sensitivity.Sensitive);
        }

       [Test]
        public void ExtractMembersToLog_CheckMemberNameWithDepth_ShouldExtractNameCorrectly()
        {
            var depthDegree3Generic = new GenericMockData<GenericMockData<MockData>>(new GenericMockData<MockData>(new MockData()));

            var extractedMembers = _extractor.ExtractMembersToLog(depthDegree3Generic);
            extractedMembers.Count().ShouldBe(3);

            extractedMembers.ShouldContain(x => x.Name == "GenericType_GenericType");
            extractedMembers.ShouldContain(x => x.Name == "GenericType_FieldNonSensitive");
            extractedMembers.ShouldContain(x => x.Name == "FieldNonSensitive");            
        }

        [Test]
        public void LoadTest()
        {
            var people = GeneratePeople(10000).ToList();
            var numOfProperties = DissectPropertyInfoMetadata.GetMemberWithSensitivity(new PersonMockData());
            var stopWatch = new Stopwatch();

            stopWatch.Start();

            foreach (var person in people)
            {
                var tmpParams = _extractor.ExtractMembersToLog(person);
                tmpParams.Count().ShouldBe(numOfProperties.Count());
            }
            stopWatch.Stop();
        }

        #region Non/Sensitive attribute Hierarchy Tests

        [Test]
        public void ExtractMembersToLog_SensitiveGenericNonSensitivePayload_NonSensitive()
        {
            var genericWithInheritance = new SensitiveGeneric<NonSensitivePayload>(new NonSensitivePayload());

            var extractedMembers = _extractor.ExtractMembersToLog(genericWithInheritance);

            extractedMembers.ShouldContain(x => x.Name == "GenericType_NonSensitiveField" && x.Value.ToString() == "NonSensitiveFieldValue" && x.Sensitivity == Sensitivity.NonSensitive);
        }

        [Test]
        public void ExtractMembersToLog_SensitiveGenericSensitivePayload_Sensitive()
        {
            var genericWithInheritance = new SensitiveGeneric<SensitivePayload>(new SensitivePayload());

            var extractedMembers = _extractor.ExtractMembersToLog(genericWithInheritance);

            extractedMembers.ShouldContain(x => x.Name == "GenericType_SensitiveField" && x.Value.ToString() == "SensitiveFieldValue" && x.Sensitivity == Sensitivity.Sensitive);
        }

        [Test]
        public void ExtractMembersToLog_NonSensitiveGenericSensitivePayload_Sensitive()
        {
            var genericWithInheritance = new NonSensitiveGeneric<SensitivePayload>(new SensitivePayload());

            var extractedMembers = _extractor.ExtractMembersToLog(genericWithInheritance);

            extractedMembers.ShouldContain(x => x.Name == "GenericType_SensitiveField" && x.Value.ToString() == "SensitiveFieldValue" && x.Sensitivity == Sensitivity.Sensitive);
        }

        [Test]
        public void ExtractMembersToLog_NonSensitiveGenericNonSensitivePayload_NonSensitive()
        {
            var genericWithInheritance = new NonSensitiveGeneric<NonSensitivePayload>(new NonSensitivePayload());

            var extractedMembers = _extractor.ExtractMembersToLog(genericWithInheritance);

            extractedMembers.ShouldContain(x => x.Name == "GenericType_NonSensitiveField" && x.Value.ToString() == "NonSensitiveFieldValue" && x.Sensitivity == Sensitivity.NonSensitive);
        }

        #endregion

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

            #region Should be Ignored
            [NonSensitive]
            private string PrivateFieldNonSensitive = "PrivateFieldName";

            [Sensitive(Secretive = false)]

            private string PrivateFieldSensitive = "PrivateFieldSensitive";

            [Sensitive(Secretive = true)]

            private string PrivateFieldCryptic = "PrivateFieldCryptic";

            #endregion

            #region Log Fields
            [NonSensitive]
            public string FieldNonSensitive = "FieldName";

            [NonSensitive]
            public JObject JObjectFieldNonSensitive = JObject.Parse("{a:1}");

            [Sensitive(Secretive = false)]

            public string FieldSensitive = "FieldSensitive";

            [Sensitive(Secretive = true)]

            public string FieldCryptic = "FieldCryptic";
            #endregion

            #region PUBLIC Properties

            public int ID { get; set; } = 10;

            [NonSensitive]
            public string Name { get; set; } = "Mocky";

            public bool IsMale { get; set; } = false;

            [Sensitive(Secretive = false)]

            public bool Sensitive { get; set; } = true;

            [Sensitive(Secretive = true)]

            public bool Cryptic { get; set; } = true;
            #endregion

            #region Ignored Properties - Should be Ignored

            private int PrivateID { get; set; } = 10;

            [NonSensitive]
            private string PrivateName { get; set; } = "Mocky";

            private bool PrivateIsMale { get; set; } = false;

            [Sensitive(Secretive = false)]

            private bool PrivateSensitive { get; set; } = true;

            [Sensitive(Secretive = true)]

            private bool PrivateCryptic { get; set; } = true;
            #endregion



            [NonSensitive]
            public string PublicWithoutGet
            {
                set => value = "Mocky";
            }

            //Redundant Members - Should be Ignored
            public void ShouldBeIgnoreMetho()
            {

            }

        }

        private class GenericMockData<T>
        {
            public GenericMockData(T genericType)
            {
                GenericType = genericType;
            }

            public T GenericType;

            [NonSensitive]
            public string FieldNonSensitive = "FieldNonSensitiveValue";
        }

        private class MockData
        {
            [Sensitive]
            public string GenericPropertySensitive { get; set; } = "GenericPropertySensitiveValue";

            [Sensitive(Secretive = true)]
            public string GenericFieldSecretive = "GenericFieldSecretiveValue";
        }

        private class TeacherWithExceptionMock : PersonMockData
        {
            public string GetError => throw new Exception("ddsfsdfasd");

            [NonSensitive]
            public int Years { get; set; }
        }

        
        private class GenericWithInheritance<T> where T : MockData
        {
            public GenericWithInheritance(T generic)
            {
                GenericType = generic;
            }
            
            public T GenericType;

            [Sensitive]
            public string GenericWithInheritancePropertySensitive { get; set; } = "GenericWithInheritancePropertySensitiveValue";
        }

        private class DerivedMockData : MockData
        {
            [Sensitive]
            public string DerivedPropertySensitive { get; set; } = "DerivedPropertySensitiveValue";
        }

        private class SensitiveGeneric<T> 
        {
            public SensitiveGeneric(T generic)
            {
                GenericType = generic;
            }

            [Sensitive]
            public T GenericType;
        }

        private class NonSensitiveGeneric<T>
        {
            public NonSensitiveGeneric(T generic)
            {
                GenericType = generic;
            }

            [NonSensitive]
            public T GenericType;
        }

        private class SensitivePayload
        {
            [Sensitive] public string SensitiveField = "SensitiveFieldValue";
        }

        private class NonSensitivePayload
        {
            [NonSensitive] public string NonSensitiveField = "NonSensitiveFieldValue";
        }

        #endregion
    }
}

