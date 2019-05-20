using System;
using Gigya.Microdot.SharedLogic;
using Ninject;

namespace Gigya.Microdot.Ninject
{
    public class MicrodotInitializer : IDisposable
    {
        public MicrodotInitializer(string appName, Action<IKernel> additionalBindings = null)
        {
            Kernel = new StandardKernel();
            Kernel.Load<MicrodotModule>();
               
            // Set custom Binding 
            additionalBindings?.Invoke(Kernel);
              
            Kernel.Get<CurrentApplicationInfo>().Init(appName);
            Kernel.Get<SystemInitializer.SystemInitializer>().Init();
        }

        public IKernel Kernel { get; private set; }

        public void Dispose()
        {
            Kernel?.Dispose();
        }
        
    }
}