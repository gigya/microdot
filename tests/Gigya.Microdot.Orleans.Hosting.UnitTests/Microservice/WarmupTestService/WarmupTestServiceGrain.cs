using System;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    [HttpService(6556)]
    public interface IWarmupTestService
    {
        Task<DateTime> CreateDate();
        Task<DateTime> DependencyCreateDate();
    }

    public interface IWarmupTestServiceGrain : IWarmupTestService, IGrainWithIntegerKey
    {
    }

    public class WarmupTestServiceGrain : Grain, IWarmupTestServiceGrain
    {
        private readonly ISingletonDependency _singletonDependency;
        private readonly DateTime _initTime;

        public WarmupTestServiceGrain(ISingletonDependency singletonDependency)
        {
            _initTime = DateTime.Now;
            _singletonDependency = singletonDependency;
        }

        public Task<DateTime> CreateDate()
        {
            return Task.FromResult(_initTime);
        }

        public Task<DateTime> DependencyCreateDate()
        {
            return Task.FromResult(_singletonDependency.CreateDate());
        }
    }

    public interface ISingletonDependency
    {
        DateTime CreateDate();
    }

    public class SingletonDependency : ISingletonDependency
    {
        public DateTime InitTime;

        public SingletonDependency()
        {
            InitTime = DateTime.Now;
        }

        public DateTime CreateDate()
        {
            return InitTime;
        }
    }
}
