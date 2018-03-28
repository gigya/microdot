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
using NUnit.Framework;
using Gigya.ServiceContract.Attributes;
using NSubstitute;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class ReflectionMetaDataExtensionTests
    {
        private int _numOfProperties;
        private ILog _logMocked;

        [SetUp]
        public void OneTimeSetup()
        {
            _numOfProperties = typeof(PersonMockData).GetProperties().Length;
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
        public void ExtracPropertiesValues_ExtractDataFromObject_ShouldBeEquivilent2()
        {
            var mock = new PersonMockData();
            var reflectionMetadataInfos = PropertiesMetadataPropertiesCache.ExtracPropertiesMetadata(mock, mock.GetType()).ToList();

            reflectionMetadataInfos.Count.ShouldBe(_numOfProperties);

            foreach (var reflectionMetadata in reflectionMetadataInfos)
            {
                var propertyInfo = typeof(PersonMockData).GetProperty(reflectionMetadata.PropertyName);

                var result = reflectionMetadata.ValueExtractor(mock);

                if (propertyInfo.GetValue(mock).Equals(result) == false)
                {
                    throw new InvalidDataException($"Propery name {propertyInfo.Name} doesn't exists.");
                }
            }
        }

        [Test]
        public void ExtracPropertiesValues_ExtractDataFromObject_ShouldBeEquivilent()
        {
            var mock = new PersonMockData();
            var reflectionMetadataInfos = PropertiesMetadataPropertiesCache.ExtracPropertiesMetadata(mock, mock.GetType()).ToList();

            reflectionMetadataInfos.Count.ShouldBe(_numOfProperties);

            foreach (var reflectionMetadata in reflectionMetadataInfos)
            {
                var propertyInfo = typeof(PersonMockData).GetProperty(reflectionMetadata.PropertyName);

                var result = reflectionMetadata.ValueExtractor(mock);

                if (propertyInfo.GetValue(mock).Equals(result) == false)
                {
                    throw new InvalidDataException($"Propery name {propertyInfo.Name} doesn't exists.");
                }
            }
        }

        [Test]
        public void ExtracPropertiesValues_ExtractSensitiveAndCryptic_ShouldBeEquivilent()
        {
            const string crypticPropertyName = nameof(PersonMockData.Cryptic);
            const string sensitivePropertyName = nameof(PersonMockData.Sensitive);

            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var mock = new PersonMockData();
            var arguments = cache.ParseIntoParams(mock);

            foreach (var arg in arguments.Where(x => x.Sensitivity != null))
            {
                if (arg.Name == crypticPropertyName)
                {
                    arg.Sensitivity.ShouldBe(Sensitivity.Secretive);
                    typeof(PersonMockData).GetProperty(crypticPropertyName).GetValue(mock).ShouldBe(mock.Cryptic);
                }

                if (arg.Name == sensitivePropertyName)
                {
                    arg.Sensitivity.ShouldBe(Sensitivity.Sensitive);
                    typeof(PersonMockData).GetProperty(sensitivePropertyName).GetValue(mock).ShouldBe(mock.Sensitive);
                }
            }
        }


        [Test]
        public void PropertyMetadata_Extract_All_Public_Properties()
        {
            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var mock = new PersonMockData();
            var arguments = cache.ParseIntoParams(mock).ToList();

            arguments.Count.ShouldBe(_numOfProperties);
            foreach (var param in arguments)
            {
                var propertyInfo = typeof(PersonMockData).GetProperty(param.Name);

                if (propertyInfo.GetValue(mock).ToString().Equals(param.Value.ToString()) == false)
                {
                    throw new InvalidDataException($"Propery name {propertyInfo.Name} doesn't exists.");
                }
            }
        }


        [Test]
        public void ExtracPropertiesValues_ExtractSensitiveAndCrypticWithInheritenceAndException_ShouldBeEquivilent()
        {
            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var mock = new TeacherWithExceptionMock();
            var arguments = cache.ParseIntoParams(mock);

            arguments.Count().ShouldBe(mock.GetType().GetProperties().Length - 1);

            ArgumentVerivications(mock,arguments);

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

            personArguments.Count().ShouldBe(person.GetType().GetProperties().Length);

            ArgumentVerivications(person, personArguments);
            _logMocked.DidNotReceive().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());

            teacherArguments.Count().ShouldBe(teacher.GetType().GetProperties().Length - 1);
            ArgumentVerivications(teacher, teacherArguments);

            _logMocked.Received().Warn(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
        }

        [Test]
        public void LoadTest()
        {
            var cache = new PropertiesMetadataPropertiesCache(_logMocked);
            var people = GeneratePeople(10000).ToList();
            var stopWatch = new Stopwatch();

            stopWatch.Start();

            foreach (var person in people)
            {
                var tmpParams = cache.ParseIntoParams(person);
                tmpParams.Count().ShouldBe(_numOfProperties);
            }
            stopWatch.Stop();
        }

        private void ArgumentVerivications(object instance, IEnumerable<MetadataCacheParam> arguments)
        {
            foreach (var arg in arguments)
            {
                switch (arg.Sensitivity)
                {

                    case Sensitivity.Secretive:

                        Varification<SensitiveAttribute>(instance, arg.Name, arg.Value, arg.Sensitivity);
                        break;

                    case Sensitivity.Sensitive:

                        Varification<SensitiveAttribute>(instance, arg.Name, arg.Value, arg.Sensitivity);
                        break;

                    case Sensitivity.NonSensitive:

                        Varification<NonSensitiveAttribute>(instance, arg.Name, arg.Value, arg.Sensitivity);
                        break;

                    default:

                        //With no attributes.
                        instance.GetType().GetProperty(arg.Name).GetCustomAttributes().Count().ShouldBe(0);

                        if (TryGetValue(instance, arg.Name, out var value))
                        {
                            value.ShouldBe(value);
                            break;
                        }
                        throw new NotImplementedException();
                }
            }
        }

        private void Varification<TAttribute>(object mock, string propName, object value, Sensitivity? sensitivity) where TAttribute : Attribute
        {
            var attribute = GetAttribute<TAttribute>(mock, propName);
            object tmpValue;

            if (TryGetValue(mock, propName, out tmpValue))
            {
                value.ShouldBe(value);
            }
            else
            {
                throw new NotImplementedException();
            }

            if (attribute is null == false)
            {
                if (attribute is SensitiveAttribute)
                {
                    (attribute as SensitiveAttribute).Secretive.ShouldBe(sensitivity == Sensitivity.Secretive);
                }
                else
                {
                    if (attribute is NonSensitiveAttribute)
                    {
                        sensitivity.ShouldBe(Sensitivity.NonSensitive);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }
        }

        private TAttribute GetAttribute<TAttribute>(object instance, string propName) where TAttribute : Attribute
        {
            var type = instance.GetType();
            var attribute = default(TAttribute);

            try
            {
                return type.GetProperty(propName).GetCustomAttribute<TAttribute>();
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private bool TryGetValue(object instance, string name, out object value)
        {
            var type = instance.GetType();
            try
            {
                value = type.GetProperty(name).GetValue(instance);
                return true;
            }
            catch (Exception e)
            {
                value = null;

            }

            return false;
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
            public int ID { get; set; } = 10;

            [NonSensitive]
            public string Name { get; set; } = "Mocky";

            public bool IsMale { get; set; } = false;

            [Sensitive(Secretive = false)]

            public bool Sensitive { get; set; } = true;

            [Sensitive(Secretive = true)]

            public bool Cryptic { get; set; } = true;

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

