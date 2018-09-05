using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    [HttpService(6556)]
    public interface IWarmupTestService
    {
        Task<int> Test();
    }

    public interface IWarmupTestServiceGrain : IWarmupTestService, IGrainWithIntegerKey
    {

    }

    public class WarmupTestServiceGrain : Grain, IWarmupTestServiceGrain
    {
        private DependantClass _dependantClass;

        public WarmupTestServiceGrain(DependantClass dependantClass)
        {
            _dependantClass = dependantClass;
        }

        public async Task<int> Test()
        {
            return 2;
        }
    }

    public class DependantClass
    {
        public int SleepTime = 5000;

        public DependantClass()
        {
            Thread.Sleep(SleepTime);
        }
    }
}
