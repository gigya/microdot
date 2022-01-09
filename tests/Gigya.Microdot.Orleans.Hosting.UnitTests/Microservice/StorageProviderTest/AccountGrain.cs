using Orleans;
using Orleans.Providers;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.StorageProviderTest
{
    [StorageProvider(ProviderName = MemoryServiceHost.MemoryStorageProvider)]
    public class AccountGrain : Grain<Account>,IAccountGrain
    {
        public async Task Save(Account account)
        {
            State = account;
            await WriteStateAsync();
        }


        public async Task<Account> Get()
        {
            await ReadStateAsync();
            return State;
        }


        public async Task Delete()
        {
            await ClearStateAsync();
        }
    }
}