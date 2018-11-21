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
            return DependantClassFake.WarmedTimes;
        }
    }

    public interface IDependantClassFake
    {
        void IncreaseWarmedTimes();
        bool ThisClassIsWarmed();
    }

    public class DependantClassFake : IDependantClassFake
    {
        public static int WarmedTimes { get; private set; }

        public static void ResetWarmedTimes()
        {
            WarmedTimes = 0;
        }

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
