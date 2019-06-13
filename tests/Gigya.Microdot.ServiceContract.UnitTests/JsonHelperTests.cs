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
using System.Linq;
using System.Numerics;
using Gigya.ServiceContract.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Common.Contracts.UnitTests
{
    public enum MyEnum { Zero, One, Two }
    public class SomeClass { public int A; public long B; public ushort? C; [JsonProperty] SomeClass Inner; }
    public struct SomeStruct { public int A; public long B; public ushort? C; }

    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class JsonHelperTests
    {
        [Test]
        public void ConvertWeaklyTypedValue_NullTargetType_ShouldThrow()
        {
            Should.Throw<ArgumentNullException>(() => JsonHelper.ConvertWeaklyTypedValue(5, null));
        }


        [Test]
        public void ConvertWeaklyTypedValue_Null_ShouldReturnNull()
        {
            object actual = JsonHelper.ConvertWeaklyTypedValue(null, typeof(MyEnum));
            actual.ShouldBe(null);
        }


        [Test]
        public void ConvertWeaklyTypedValue_EnumAsNumeric_ShouldConvert()
        {
            object actual = JsonHelper.ConvertWeaklyTypedValue((long)MyEnum.Two, typeof(MyEnum));
            actual.ShouldBe(MyEnum.Two);
        }


        [Test]
        public void ConvertWeaklyTypedValue_EnumAsInvalidNumeric_ShouldConvert()
        {
            object actual = JsonHelper.ConvertWeaklyTypedValue((long)5, typeof(MyEnum));
            actual.ShouldBe((MyEnum)5);
        }


        [Test]
        public void ConvertWeaklyTypedValue_EnumAsNullableNumeric_ShouldConvert()
        {
            object actual = JsonHelper.ConvertWeaklyTypedValue((long)MyEnum.Two, typeof(MyEnum?));
            actual.ShouldBe(MyEnum.Two);
        }


        [Test]
        public void ConvertWeaklyTypedValue_EnumAsInvalidNullableNumeric_ShouldConvert()
        {
            object actual = JsonHelper.ConvertWeaklyTypedValue((long)5, typeof(MyEnum?));
            actual.ShouldBe((MyEnum)5);
        }


        [Test]
        public void ConvertWeaklyTypedValue_EnumAsString_ShouldConvert()
        {
            object actual = JsonHelper.ConvertWeaklyTypedValue("Two", typeof(MyEnum));
            actual.ShouldBe(MyEnum.Two);
        }


        [Test]
        public void ConvertWeaklyTypedValue_EnumAsInvalidString_ShouldThrow()
        {
            Should.Throw<Exception>(() => JsonHelper.ConvertWeaklyTypedValue("INVALID", typeof(MyEnum)));
        }


        [TestCase("N"), TestCase("D"), TestCase("B"), TestCase("P")]
        public void ConvertWeaklyTypedValue_GuidAsString_ShouldConvert(string format)
        {
            var expected = Guid.NewGuid();
            object actual = JsonHelper.ConvertWeaklyTypedValue(expected.ToString(format), typeof(Guid));
            actual.ShouldBe(expected);
        }


        [Test]
        public void ConvertWeaklyTypedValue_LargeUInt64AsBigInteger_ShouldConvert()
        {
            object actual = JsonHelper.ConvertWeaklyTypedValue(new BigInteger(ulong.MaxValue), typeof(ulong));
            actual.ShouldBeOfType<ulong>();
            actual.ShouldBe(ulong.MaxValue);
        }


        [Test]
        public void ConvertWeaklyTypedValue_LocalDateTimeAsString_ShouldConvert()
        {
            var expected = DateTime.Now;
            DateTime actual = (DateTime)JsonHelper.ConvertWeaklyTypedValue(expected.ToString("O"), typeof(DateTime));
            actual.ShouldBe(expected);
            actual.Kind.ShouldBe(DateTimeKind.Local);
        }


        [Test]
        public void ConvertWeaklyTypedValue_LocalDateTimeAsDateTimeOffset_ShouldConvert()
        {
            var expected = DateTime.Now;
            DateTime actual = (DateTime)JsonHelper.ConvertWeaklyTypedValue(new DateTimeOffset(expected), typeof(DateTime));
            actual.ShouldBe(expected);
            actual.Kind.ShouldBe(DateTimeKind.Local);
        }


        [Test]
        public void ConvertWeaklyTypedValue_UtcDateTimeAsString_ShouldConvert()
        {
            var expected = DateTime.UtcNow;
            DateTime actual = (DateTime)JsonHelper.ConvertWeaklyTypedValue(expected.ToString("O"), typeof(DateTime));
            actual.ShouldBe(expected);
            actual.Kind.ShouldBe(DateTimeKind.Utc);
        }


        [Test]
        public void ConvertWeaklyTypedValue_UtcDateTimeAsDateTimeOffset_ShouldConvert()
        {
            var expected = DateTime.UtcNow;
            DateTime actual = (DateTime)JsonHelper.ConvertWeaklyTypedValue(new DateTimeOffset(expected), typeof(DateTime));
            actual.ShouldBe(expected);
            actual.Kind.ShouldBe(DateTimeKind.Utc);
        }


        [Test]
        public void ConvertWeaklyTypedValue_DateTimeAsInt_ShouldThrow()
        {
            Should.Throw<Exception>(() => JsonHelper.ConvertWeaklyTypedValue(5, typeof(DateTime)));
        }


        [Test]
        public void ConvertWeaklyTypedValue_LocalDateTimeOffsetAsString_ShouldConvert()
        {
            var expected = DateTimeOffset.Now;
            DateTimeOffset actual = (DateTimeOffset)JsonHelper.ConvertWeaklyTypedValue(expected.ToString("O"), typeof(DateTimeOffset));
            actual.ShouldBe(expected);
            actual.Offset.ShouldBe(expected.Offset);
        }


        [Test]
        public void ConvertWeaklyTypedValue_LocalDateTimeOffsetAsDateTime_ShouldConvert()
        {
            var expected = DateTimeOffset.Now;
            DateTimeOffset actual = (DateTimeOffset)JsonHelper.ConvertWeaklyTypedValue(expected.LocalDateTime, typeof(DateTimeOffset));
            actual.ShouldBe(expected);
            actual.Offset.ShouldBe(expected.Offset);
        }


        [Test]
        public void ConvertWeaklyTypedValue_UtcDateTimeOffsetAsString_ShouldConvert()
        {
            var expected = DateTimeOffset.UtcNow;
            DateTimeOffset actual = (DateTimeOffset)JsonHelper.ConvertWeaklyTypedValue(expected.ToString("O"), typeof(DateTimeOffset));
            actual.ShouldBe(expected);
            actual.Offset.ShouldBe(expected.Offset);
        }


        [Test]
        public void ConvertWeaklyTypedValue_UtcDateTimeOffsetAsDateTime_ShouldConvert()
        {
            var expected = DateTimeOffset.UtcNow;
            DateTimeOffset actual = (DateTimeOffset)JsonHelper.ConvertWeaklyTypedValue(expected.UtcDateTime, typeof(DateTimeOffset));
            actual.ShouldBe(expected);
            actual.Offset.ShouldBe(expected.Offset);
        }


        [Test]
        public void ConvertWeaklyTypedValue_ForeignDateTimeOffsetAsString_ShouldConvert()
        {
            var expected = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified), TimeSpan.FromHours(6));
            DateTimeOffset actual = (DateTimeOffset)JsonHelper.ConvertWeaklyTypedValue(expected.ToString("O"), typeof(DateTimeOffset));
            actual.ShouldBe(expected);
            actual.Offset.ShouldBe(expected.Offset);
        }


        [Test]
        public void ConvertWeaklyTypedValue_DateTimeOffsetAsInt_ShouldThrow()
        {
            Should.Throw<Exception>(() => JsonHelper.ConvertWeaklyTypedValue(5, typeof(DateTimeOffset)));
        }


        [Test]
        public void ConvertWeaklyTypedValue_TimeSpanAsString_ShouldConvert()
        {
            var expected = new TimeSpan(11, 12, 13, 14, 15);
            object actual = JsonHelper.ConvertWeaklyTypedValue(expected.ToString(), typeof(TimeSpan));
            actual.ShouldBe(expected);
        }


        [Test]
        public void ConvertWeaklyTypedValue_TimeSpanAsInvalidString_ShouldConvert()
        {
            Should.Throw<InvalidParameterValueException>(() => JsonHelper.ConvertWeaklyTypedValue("INVALID", typeof(TimeSpan)));
        }


        [Test]
        public void ConvertWeaklyTypedValue_SomeClassAsJson_ShouldConvert()
        {
            var expected = new SomeClass { A = 5, B = 109 };
            var actual = JsonHelper
                .ConvertWeaklyTypedValue(JsonConvert.SerializeObject(expected), typeof(SomeClass))
                .ShouldBeOfType<SomeClass>();
            actual.A.ShouldBe(expected.A);
            actual.B.ShouldBe(expected.B);
            actual.C.ShouldBe(expected.C);
        }


        [Test]
        public void ConvertWeaklyTypedValue_SomeStructAsJson_ShouldConvert()
        {
            var expected = new SomeStruct { A = 5, B = 109 };
            var actual = JsonHelper
                .ConvertWeaklyTypedValue(JsonConvert.SerializeObject(expected), typeof(SomeClass))
                .ShouldBeOfType<SomeClass>();
            actual.A.ShouldBe(expected.A);
            actual.B.ShouldBe(expected.B);
            actual.C.ShouldBe(expected.C);
        }
    }



    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class JsonHelperNumericTests
    {
        [DatapointSource]
        private Type[] GetTypes() => new[]
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal)
        };


        [DatapointSource]
        private object[] GetValues() => GetTypes().SelectMany(t => new[]
        {
            Convert.ChangeType(42, t),
            t.GetField("MinValue").GetValue(null),
            t.GetField("MaxValue").GetValue(null),
            null
        }).ToArray();


        [Theory]
        public void ConvertWeaklyTypedValue_NumericValue(object value, Type targetType)
        {
            if (value == null || (value is double && targetType == typeof(float)))
                return;

            try
            {
                object actual = JsonHelper.ConvertWeaklyTypedValue(value, targetType);
                actual.ShouldBeOfType(Nullable.GetUnderlyingType(targetType) ?? targetType);
                actual.ShouldBe(value);
            }
            catch (InvalidParameterValueException) { }
        }


        [Theory]
        public void ConvertWeaklyTypedValue_NullableNumericValue(object value, Type targetType)
        {
            if (value is double && targetType == typeof(float))
                return;

            try
            {
                targetType = typeof(Nullable<>).MakeGenericType(targetType);
                object actual = JsonHelper.ConvertWeaklyTypedValue(value, targetType);

                if (value != null)
                    actual.ShouldBeOfType(Nullable.GetUnderlyingType(targetType) ?? targetType);

                actual.ShouldBe(value);
            }
            catch (InvalidParameterValueException) { }
        }

        [Test]
        public void ComplexObjectWithNullValue_ThrowException()
        {
            var json = JObject.Parse(@"{A:null}"); 
            try
            {
                JsonHelper.ConvertWeaklyTypedValue(json, typeof(SomeClass));
                Assert.Fail("Should throw exception because field 'A' is null");
            }
            catch (InvalidParameterValueException ex)
            {
                ex.parameterName.ShouldBeNull();
                ex.ErrorPath.SequenceEqual(new[] {"A"});
            }
        }

        [Test]
        public void ComplexObjectWithWrongValue_ThrowException()
        {
            var json = JObject.Parse(@"{A:""abcd""}");
            try
            {
                JsonHelper.ConvertWeaklyTypedValue(json, typeof(SomeClass));
                Assert.Fail("Should throw exception because field 'A' has invalid value");
            }
            catch (InvalidParameterValueException ex)
            {
                ex.parameterName.ShouldBeNull();
                ex.ErrorPath.SequenceEqual(new[] { "A" });
            }
        }

        [Test]
        public void ComplexObjectWithComplexObjectWithNullValue_ThrowException()
        {
            var json = JObject.Parse(@"{Inner: {A:null}}");
            try
            {
                JsonHelper.ConvertWeaklyTypedValue(json, typeof(SomeClass));
                Assert.Fail("Should throw exception because field 'Inner.A' is null");
            }
            catch (InvalidParameterValueException ex)
            {
                ex.parameterName.ShouldBeNull();
                ex.ErrorPath.SequenceEqual(new[] { "Inner", "A" });
            }
        }

        [Test]
        public void ComplexObjectWithComplexObjectWithWrongValue_ThrowException()
        {
            var json = JObject.Parse(@"{Inner: {A:""abcd""}}");
            try
            {
                JsonHelper.ConvertWeaklyTypedValue(json, typeof(SomeClass));
                Assert.Fail("Should throw exception because field 'Inner.A' has invalid value");
            }
            catch (InvalidParameterValueException ex)
            {
                ex.parameterName.ShouldBeNull();
                ex.ErrorPath.SequenceEqual(new[] { "Inner","A" });
            }
        }

    }
}
