using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService
{
    public sealed class DirectWorker : IWorker
    {
        public void FireAndForget(Func<Task> asyncAction)
        {
            asyncAction();
        }


        public void Dispose() {  }
    }
}