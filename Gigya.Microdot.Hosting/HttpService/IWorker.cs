using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService
{
    public interface IWorker:IDisposable
    {
        void FireAndForget(Func<Task> asyncAction);
    }
}
