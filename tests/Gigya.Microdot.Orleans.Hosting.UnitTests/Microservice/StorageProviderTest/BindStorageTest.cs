using System;
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
            var account = await accountGrain.Get();
            Assert.AreEqual("test", account.Name);
            try
            {
                selfHostService.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
