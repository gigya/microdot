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
using System.Collections.Generic;
using System.Linq;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Logging;
using Ninject;
using Orleans;
using Orleans.Core;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Ninject.Host
{
    public class GrainsWarmup : IWarmup
    {
        private IServiceInterfaceMapper _orleansMapper;
        private IKernel _kernel;
        private ILog _log;
        private List<Type> _orleansInternalTypes = new List<Type>();
        private  List<string> _orleansInternalTypesString = new List<string>();

        public GrainsWarmup(IServiceInterfaceMapper orleansMapper, IKernel kernel, ILog log)
        {
            _orleansMapper = orleansMapper;
            _kernel = kernel;
            _log = log;

            _orleansInternalTypes.Add(typeof(IGrainFactory));
            _orleansInternalTypes.Add(typeof(IGrainState));
            _orleansInternalTypes.Add(typeof(IGrainIdentity));
            _orleansInternalTypes.Add(typeof(IGrainRuntime));
            _orleansInternalTypesString.Add("OrleansDashboard");
            _orleansInternalTypesString.Add("Orleans.IReminderTable");
        }

        public void Warmup()
        {
            // We cannot use Orleans to obtain grains (to warm them up); Orleans only provides lazy grain references, and we'd
            // have to call a method on the grain to activate it, but there's no generic method we could call. So we instantiate
            // grains through Ninject instead of Orleans to register their dependencies in Ninject. When Orleans will instantiate
            // these grains, their non-transient dependencies will already be registered in Ninject, saving startup time.
            try
            {
                List<string> failedWarmupWarn = new List<string>();
                foreach (Type serviceClass in _orleansMapper.ServiceClassesTypes)
                {
                    try
                    {
                        foreach (Type parameterType in serviceClass.GetConstructors()
                            .SelectMany(ctor => ctor.GetParameters().Select(p => p.ParameterType)).Distinct())
                        {
                            try
                            {
                              

                                if (!_kernel.CanResolve(parameterType))
                                {
                                    if (_orleansInternalTypes.Contains(parameterType) || _orleansInternalTypesString.Any(x => parameterType.FullName.Contains(x)))
                                    {
                                        //No  waring on Orleans type
                                        continue;
                                    }

                                    failedWarmupWarn.Add($"Type {parameterType} of grain {serviceClass}");

                                    continue;
                                }

                                // Try to warm up dependency 
                                _kernel.Get(parameterType);
                            }
                            catch//No exception handling needed. We try to warmup all constructor types. In case of failure, write the warning for non orleans types and go to the next type
                            {
                                failedWarmupWarn.Add($"Type {parameterType} of grain {serviceClass}");
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        _log.Warn($"Failed to warmup grain {serviceClass}", e);

                    }
                }

                if (failedWarmupWarn.Count > 0)
                {
                    _log.Warn($"Fail to warmup the following types:\n{string.Join("\n", failedWarmupWarn)}");
                }
            }

            catch (Exception ex)
            {
                _log.Warn("Failed to warmup grains", ex);
            }
        }

    }
}
    
