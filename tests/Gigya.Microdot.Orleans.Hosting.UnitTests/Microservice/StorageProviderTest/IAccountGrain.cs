using Orleans;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.StorageProviderTest
{
    public interface IAccountGrain : IGrainWithIntegerKey
    {
        Task Save(Account account);

        Task<Account> Get();

        Task Delete();
    }
}