using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService
{
    public interface IWarmup
    {
        Task WaitForWarmup();

        void Warmup();
    }
}
