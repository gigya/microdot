using System;
using System.Linq;
using System.Threading.Tasks;

using Gigya.Microdot.ServiceContract.HttpService;
using Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice.CalculatorService;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.Testing.ServiceTester;

using NUnit.Framework;
using Gigya.Microdot.ServiceContract.Attributes;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests
{
    [TestFixture]
    public class SchemaEndpointTests
    {
        private ServiceProxyProvider _serviceProxyProvider;
        private ServiceTester<CalculatorServiceHost> _tester;


        [OneTimeSetUp]
        public void SetUp()
        {
            try
            {
                _tester = AssemblyInitialize.ResolutionRoot.GetServiceTester<CalculatorServiceHost>();
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
            var attribute = schemaTestMethod.Attributes.FirstOrDefault(_ => _.Attribute is PublicEndpointAttribute);
            Assert.IsNotNull(attribute, "method GetAppDomainChain should include attribute of type PublicEndpoint");
            Assert.IsTrue(((PublicEndpointAttribute)attribute.Attribute).EndpointName != null, $"PublicEndpoint attribute of SchemaTestMethod should include '{nameof(PublicEndpointAttribute.EndpointName)}' property");
            Assert.AreEqual("test.calculator.getAppDomainChain", ((PublicEndpointAttribute)attribute.Attribute).EndpointName);
        }

    }

}
