using System.Collections;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService
{
    public interface IActivator
    {
        Task<InvocationResult> Invoke(ServiceMethod serviceMethod, IDictionary args);

        Task<InvocationResult> Invoke(ServiceMethod serviceMethod, object[] arguments);
    }
}