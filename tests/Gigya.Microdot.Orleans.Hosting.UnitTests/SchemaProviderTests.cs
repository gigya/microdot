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
using System.IO;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.Testing.Shared;
using Newtonsoft.Json;
using Ninject;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture]
    public class SchemaProviderTests
    {
        private TestingKernel<ConsoleLog> _kernel;
        private Type[] _services;

        [OneTimeSetUp]
        public void SetUp()
        {
            try
            {
                _kernel = new TestingKernel<ConsoleLog>((kernel) =>
                {
                    kernel.Rebind<IServiceInterfaceMapper>().ToMethod(_=> new IdentityServiceInterfaceMapper(_services));                    
                });
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
            _kernel.Dispose();
        }

        [Test]
        public void ReturnSameHashCodeForSameSchema()
        {
            _services = new[] { typeof(ITestService) };
            var firstSchema = GetSchema();
            var secondSchema = GetSchema(); 
            Assert.AreNotEqual(firstSchema, secondSchema);
            Assert.AreEqual(firstSchema.HashCode, secondSchema.HashCode);
        }

        [Test]
        public void ReturnDifferentHashCodeForDifferentSchema()
        {
            _services = new[] {typeof(ITestService)};
            var firstSchema = GetSchema();

            _services = new[] {typeof(ICalculatorService)};
            var secondSchema = GetSchema();
            
            Assert.AreNotEqual(firstSchema.HashCode, secondSchema.HashCode);
        }

        [Test]
        public void ReturnSameHashCodeAfterSerialization()
        {
            _services = new[] { typeof(ITestService) };
            var firstSchema = GetSchema();
            var serialized = JsonConvert.SerializeObject(firstSchema);
            var secondSchema = JsonConvert.DeserializeObject<ServiceSchema>(serialized);
            Assert.AreEqual(firstSchema.HashCode, secondSchema.HashCode);
        }

        private ServiceSchema GetSchema()
        {
            _kernel.Rebind<ISchemaProvider>().To<SchemaProvider>(); // create a new instance of SchemaProvider
            var schemaProvider = _kernel.Get<ISchemaProvider>();
            return schemaProvider.Schema;
        }
    }

    [HttpService(3579)]
    public interface ITestService
    {
        Task DoNothing(string foo);
    }
}
