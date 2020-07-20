using Gigya;
using Gigya.Common;
using Gigya.Common.OrleansInfra;
using Gigya.Common.OrleansInfra.FunctionalTests;
using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Hosting.Environment;
using Gigya.Microdot.Testing.Service;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.StorageProviderTest
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class BindStorageTest
    {

        [Test]
        public async Task CanBindStorage()
        {
            ServiceTester<MemoryServiceHost> selfHostService = new ServiceTester<MemoryServiceHost>();
            
            var accountGrain = selfHostService.GrainClient.GetGrain<IAccountGrain>(0);
            await accountGrain.Save(new Account() { Name = "test" });
            var accunt = await accountGrain.Get();
            Assert.AreEqual("test", accunt.Name);
        }
    }
}
