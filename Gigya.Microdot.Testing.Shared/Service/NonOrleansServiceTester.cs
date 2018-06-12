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
using Gigya.Microdot.Hosting.Service;
using Ninject.Syntax;

namespace Gigya.Microdot.Testing.Shared.Service
{
    public class NonOrleansServiceTester<TServiceHost> : ServiceTesterBase where TServiceHost : ServiceHostBase, new()
    {

        private readonly TServiceHost _host = new TServiceHost();
        private Task _stopTask;

        public NonOrleansServiceTester(int basePortOverride, IResolutionRoot resolutionRoot, TimeSpan? shutdownWaitTime = null)
        {
            var serviceArguments = GetServiceArguments(basePortOverride, false, shutdownWaitTime.HasValue?(int?)shutdownWaitTime.Value.TotalSeconds:null);

            BasePort = basePortOverride;
            ResolutionRoot = resolutionRoot;
            _stopTask = _host.RunAsync(serviceArguments);
        }

        public override void Dispose()
        {
            _host.Stop();
            _host.Dispose();
            var completed = _stopTask.Wait(60000);

            if (!completed)
                throw new TimeoutException("ServiceTester: The service failed to shutdown within the 60 second limit.");
        }
    }
}