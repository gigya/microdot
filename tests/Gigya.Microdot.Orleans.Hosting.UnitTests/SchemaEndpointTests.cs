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
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.Testing.Service;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class SchemaEndpointTests
    {
        private IServiceProxyProvider _serviceProxyProvider;
        private ServiceTester<CalculatorServiceHost> _tester;


        [OneTimeSetUp]
        public void SetUp()
        {
            try
            {
                _tester = new ServiceTester<CalculatorServiceHost>();
                _serviceProxyProvider = _tester.GetServiceProxyProvider("CalculatorService");
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
            _tester.Dispose();
        }


        [Test]
        public async Task MethodTypeName()
        {
            var schema = await _serviceProxyProvider.GetSchema();
            var iface = schema.Interfaces.First(_ => _.Name == typeof(ICalculatorService).FullName);
            var schemaTestMethod = iface.Methods.FirstOrDefault(_ => _.Name == nameof(ICalculatorService.Add));
            Assert.IsNotNull(schemaTestMethod, "Service schema did not return the method Add");
            Assert.AreEqual(typeof(ICalculatorService).FullName, iface.Name);
        }


        [Test]
        public async Task ReturnPublicEndpointAttribute()
        {
            var schema = await _serviceProxyProvider.GetSchema();
            var iface = schema.Interfaces.First(_ => _.Name == typeof(ICalculatorService).FullName);
            var schemaTestMethod = iface.Methods.FirstOrDefault(_ => _.Name == nameof(ICalculatorService.GetAppDomainChain));
            Assert.IsNotNull(schemaTestMethod, "Service schema did not return the method GetAppDomainChain");            
            var attribute = schemaTestMethod.Attributes.Select(x => x.Attribute).OfType<PublicEndpointAttribute>().Single();
            Assert.IsNotNull(attribute, "method GetAppDomainChain should include attribute of type PublicEndpoint");
            Assert.IsTrue(attribute.EndpointName != null, $"PublicEndpoint attribute of SchemaTestMethod should include '{nameof(PublicEndpointAttribute.EndpointName)}' property");
            Assert.AreEqual("test.calculator.getAppDomainChain", attribute.EndpointName);
            Assert.AreEqual(false, attribute.RequireHTTPS);
            Assert.AreEqual("something", attribute.PropertyNameForResponseBody);
        }

    }

}
