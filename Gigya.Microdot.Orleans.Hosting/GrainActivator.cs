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
using System.Collections.Concurrent;
using Gigya.Microdot.Hosting.HttpService;
using Orleans;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class GrainActivator : AbstractServiceActivator
    {
        private Lazy<IGrainFactory> Factory { get; }
        private ConcurrentDictionary<Type, IGrain> GrainCache { get; }

        public GrainActivator(Lazy<IGrainFactory> factory)
        {
            Factory = factory;
            GrainCache = new ConcurrentDictionary<Type, IGrain>();
        }

        protected override object GetInvokeTarget(ServiceMethod serviceMethod)
        {
            return GrainCache.GetOrAdd(serviceMethod.GrainInterfaceType, t => GetGrain(serviceMethod.GrainInterfaceType));
        }

        private IGrain GetGrain(Type grainInterfaceType)
        {
          
            var getGrainMethod = typeof(IGrainFactory).GetMethod( nameof( Factory.Value.GetGrain), new[] { typeof(long), typeof(string) })
                .MakeGenericMethod(grainInterfaceType);

            return (IGrain)getGrainMethod.Invoke(Factory.Value, new object[] { 0, null });
        }
    }
}