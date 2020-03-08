using Gigya.Microdot.Common.Tests;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Hosting;
using Gigya.Microdot.Orleans.Ninject.Host;
using Ninject;
using Ninject.Syntax;
using Orleans;
using Orleans.Hosting;
using Orleans.Providers;
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