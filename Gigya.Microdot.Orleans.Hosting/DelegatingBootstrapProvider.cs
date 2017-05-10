using System;
using System.Threading.Tasks;

using Orleans.Providers;

namespace Gigya.Microdot.Orleans.Hosting
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class DelegatingBootstrapProvider : IBootstrapProvider
    {
        public static Func<IProviderRuntime, Task> OnInit { get; set; }
        public static Func<Task> OnClose { get; set; }

        public string Name => "DelegatingBootstrapProvider";


        public async Task Init(string name, IProviderRuntime providerRuntime, IProviderConfiguration config)
        {
            if (OnInit != null)
                await OnInit(providerRuntime);
        }


        public async Task Close()
        {
            if (OnClose != null)
                await OnClose();
        }
    }
}