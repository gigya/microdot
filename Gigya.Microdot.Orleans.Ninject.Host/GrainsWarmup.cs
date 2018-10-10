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
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting;
using Ninject;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    public class GrainsWarmup : IWarmup
    {
        private IServiceInterfaceMapper _orleansMapper;
        private IKernel _kernel;
        private ILog _log;

        public GrainsWarmup(IServiceInterfaceMapper orleansMapper, IKernel kernel, ILog log)
        {
            _orleansMapper = orleansMapper;
            _kernel = kernel;
            _log = log;
        }

        public void Warmup()
        {
            // We cannot use Orleans to obtain grains (to warm them up); Orleans only provides lazy grain references, and we'd
            // have to call a method on the grain to activate it, but there's no generic method we could call. So we instantiate
            // grains through Ninject instead of Orleans to register their dependencies in Ninject. When Orleans will instantiate
            // these grains, their non-transient dependencies will already be registered in Ninject, saving startup time.
            try
            {
                foreach (Type serviceClass in _orleansMapper.ServiceClassesTypes)
                {
                    _kernel.Get(serviceClass);
                }
            }
            catch(Exception ex)
            {
                _log.Error("Failed to warmup grains", ex);
                throw;
            }
        }
    }
}
