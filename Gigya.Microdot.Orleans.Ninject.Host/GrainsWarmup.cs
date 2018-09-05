#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Orleans.Hosting;
using Ninject;

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
