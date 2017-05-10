using System.Threading.Tasks;

using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Hosting.Service
{
    public static class HostExtensionMethods
    {
        public static Task RunAsync(this GigyaServiceHost host, ServiceArguments arguments = null)
        {
            var stopTask = Task.Run(() => host.Run(arguments));
            Task.WhenAny(stopTask, host.WaitForServiceStartedAsync()).GetAwaiter().GetResult().GetAwaiter().GetResult();
            return stopTask;
        }
    }
}