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
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Newtonsoft.Json;
using NUnit.Framework;
#pragma warning disable 169

namespace Gigya.Common.Contracts.UnitTests
{
    class DataParamBase
    {
        [Sensitive]
        public int BaseField;
    }
    class Data: DataParamBase
    {
        public string s;
        public Nested n;
    }

    class Nested
    {
        public DateTime time;
    }

    class ResponseData
    {
        public string a;
        public int b;
    }

    public class SensitiveAttribute : Attribute {}

    [HttpService(100)]
    internal interface ITestInterface
    {
        [PublicEndpoint("demo.doSomething")]
        Task<ResponseData> DoSomething(int i, double? nd, string s, [Sensitive] Data data);
    }

    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class ServiceSchemaTests
    {
        [Test]
        public void TestSerialization()
        {
            ServiceSchema schema = new ServiceSchema(new[] { typeof(ITestInterface) });
            string serialized = JsonConvert.SerializeObject(schema, new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });
            schema = JsonConvert.DeserializeObject<ServiceSchema>(serialized);

            Assert.IsTrue(schema.Interfaces.Length == 1);

            InterfaceSchema interfaceSchema = schema.Interfaces[0];
            Assert.IsTrue(interfaceSchema.Name == typeof(ITestInterface).FullName);
            Assert.IsTrue(interfaceSchema.Attributes.Length == 1);
            Assert.IsTrue(interfaceSchema.Attributes[0].Attribute is HttpServiceAttribute);
            Assert.IsTrue(interfaceSchema.Attributes[0].TypeName == typeof(HttpServiceAttribute).AssemblyQualifiedName);
            Assert.IsTrue(((HttpServiceAttribute)interfaceSchema.Attributes[0].Attribute).BasePort == 100);
            Assert.IsTrue(interfaceSchema.Methods.Length == 1);

            MethodSchema methodSchema = interfaceSchema.Methods[0];
            Assert.IsTrue(methodSchema.Name == nameof(ITestInterface.DoSomething));
            Assert.IsTrue(methodSchema.Attributes.Length == 1);

            AttributeSchema attributeSchema = methodSchema.Attributes[0];
            Assert.IsTrue(attributeSchema.Attribute is PublicEndpointAttribute);
            Assert.IsTrue(attributeSchema.TypeName == typeof(PublicEndpointAttribute).AssemblyQualifiedName);
            Assert.IsTrue(((PublicEndpointAttribute)attributeSchema.Attribute).EndpointName == "demo.doSomething");

            Assert.IsTrue(methodSchema.Parameters.Length == 4);
            Assert.AreEqual(0, methodSchema.Parameters[0].Attributes.Length);
            Assert.AreEqual("i", methodSchema.Parameters[0].Name);

            Assert.AreEqual(typeof(int), methodSchema.Parameters[0].Type);
            Assert.AreEqual(typeof(int).AssemblyQualifiedName, methodSchema.Parameters[0].TypeName);
            Assert.AreEqual("nd", methodSchema.Parameters[1].Name);
            Assert.AreEqual(typeof(double?), methodSchema.Parameters[1].Type);
            Assert.AreEqual(typeof(double?).AssemblyQualifiedName, methodSchema.Parameters[1].TypeName);
            Assert.AreEqual("s", methodSchema.Parameters[2].Name);


            Assert.AreEqual(1, methodSchema.Parameters[3].Attributes.Length);
            Assert.IsTrue(methodSchema.Parameters[3].Attributes[0].Attribute is SensitiveAttribute);


            Assert.AreEqual(nameof(DataParamBase.BaseField), methodSchema.Parameters[3].Fields[2].Name);
            Assert.IsTrue(methodSchema.Parameters[3].Fields[2].Attributes[0].Attribute is SensitiveAttribute);

            Assert.AreEqual(nameof(Data.s), methodSchema.Parameters[3].Fields[0].Name);
            Assert.AreEqual(typeof(string), methodSchema.Parameters[3].Fields[0].Type);

            Assert.AreEqual(nameof(Data.n), methodSchema.Parameters[3].Fields[1].Name);
            Assert.AreEqual(typeof(Nested), methodSchema.Parameters[3].Fields[1].Type);


            Assert.AreEqual(typeof(ResponseData), methodSchema.Response.Type);
            Assert.AreEqual(nameof(ResponseData.a), methodSchema.Response.Fields[0].Name);
            Assert.AreEqual(typeof(string), methodSchema.Response.Fields[0].Type);
            Assert.AreEqual(nameof(ResponseData.b), methodSchema.Response.Fields[1].Name);
            Assert.AreEqual(typeof(int), methodSchema.Response.Fields[1].Type);
        }


        [Test]
        public void TestUnknownAttribute()
        {
            var typeFullName = typeof(SensitiveAttribute).AssemblyQualifiedName;
            string json = @"
                {
                  ""TypeName"": """ + typeFullName + @""",
                  ""Data"": {
                    ""TypeId"": """ + typeFullName + @"""
                  }
                }";
            AttributeSchema attr = JsonConvert.DeserializeObject<AttributeSchema>(json);
            Assert.IsNotNull(attr.Attribute);
        
            json = @"
                {
                  ""TypeName"": ""Gigya.Microdot.ServiceContract.UnitTests.HttpService.SensitiveAttribute2, Gigya.Microdot.ServiceContract.UnitTests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",
                  ""Data"": {
                    ""TypeId"": """ + typeFullName + @"""
                  }
                }";
            attr = JsonConvert.DeserializeObject<AttributeSchema>(json);
            Assert.IsNull(attr.Attribute);
            Assert.IsTrue(attr.TypeName == "Gigya.Microdot.ServiceContract.UnitTests.HttpService.SensitiveAttribute2, Gigya.Microdot.ServiceContract.UnitTests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        }


        [Test]
        public void TestUnknownParamType()
        {
            string json = @"
                {
                  ""Name"": ""i"",
                  ""TypeName"": ""System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"",
                  ""Attributes"": []
                }";
            ParameterSchema param = JsonConvert.DeserializeObject<ParameterSchema>(json);
            Assert.IsNotNull(param.Type);

            json = @"
                {
                  ""Name"": ""i"",
                  ""TypeName"": ""System.Int33, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"",
                  ""Attributes"": []
                }";
            param = JsonConvert.DeserializeObject<ParameterSchema>(json);
            Assert.IsNull(param.Type);
            Assert.IsTrue(param.TypeName == "System.Int33, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
        }
    }
}
