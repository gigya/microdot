using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    [HttpService(6556)]
    public interface IWarmupTestService
    {
        Task<int> TestWarmedTimes();
    }

    public interface IWarmupTestServiceGrain : IWarmupTestService, IGrainWithIntegerKey
    {

    }

    public class WarmupTestServiceGrain : Grain, IWarmupTestServiceGrain
    {
        private readonly IDependantClassFake _dependantClassFake;

        public WarmupTestServiceGrain(IDependantClassFake dependantClassFake)
        {
            _dependantClassFake = dependantClassFake;
            _dependantClassFake.ThisClassIsWarmed();
        }

        public async Task<int> TestWarmedTimes()
        {
            return _dependantClassFake.WarmedTimes;
        }
    }

    public interface IDependantClassFake
    {
        int WarmedTimes { get; }
        void IncreaseWarmedTimes();
        bool ThisClassIsWarmed();
    }

    public class DependantClassFake : IDependantClassFake
    {
        public int WarmedTimes { get; private set; }

        public DependantClassFake()
        {
            IncreaseWarmedTimes();
        }

        public void IncreaseWarmedTimes()
        {
            WarmedTimes++;
        }

        public bool ThisClassIsWarmed()
        {
            return true;
        }
    }
}
