using System;
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Orleans.Hosting;
using Ninject;
using Orleans;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    public class GrainsWarmup : IWarmup
    {
        private GrainActivator _grainActivator;
        private IServiceInterfaceMapper _orleansMapper;
        private TaskCompletionSource<bool> _taskCompletionSource;
        private IKernel _kernel;

        public GrainsWarmup(IActivator grainActivator, IServiceInterfaceMapper orleansMapper, IKernel kernel)
        {
            _grainActivator = grainActivator as GrainActivator;
            _orleansMapper = orleansMapper;
            _taskCompletionSource = new TaskCompletionSource<bool>();
            _kernel = kernel;
        }

        public void Warmup()
        {
            if (!OrleansInterfaces())
            {
                return;
            }

            try
            {
                foreach (Type serviceClass in _orleansMapper.ServiceClassesTypes)
                {
                    _kernel.Get(serviceClass);
                }
            }
            catch
            {
                _taskCompletionSource.SetException(new Exception("Failed to warmup grains"));

                throw;
            }

            _taskCompletionSource.SetResult(true);
        }

        public async Task WaitForWarmup()
        {
            await _taskCompletionSource.Task;
        }

        private bool OrleansInterfaces()
        {
            return _grainActivator != null && _orleansMapper is OrleansServiceInterfaceMapper;
        }
    }
}
