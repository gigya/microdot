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

#endregion Copyright

using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.SharedLogic;
using Ninject.Syntax;
using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.Testing.Shared.Service
{
    public class NonOrleansServiceTester<TServiceHost> : ServiceTesterBase where TServiceHost : ServiceHostBase, new()
    {
        public readonly TServiceHost Host = new TServiceHost();
        private Task _siloStopped;

        public NonOrleansServiceTester(IResolutionRoot resolutionRoot, ServiceArguments serviceArguments)
        {
            ResolutionRoot = resolutionRoot;
            if(serviceArguments.BasePortOverride ==null ) 
                throw  new  ArgumentException("ServiceArguments.BasePortOverride should not be null ");

            BasePort = serviceArguments.BasePortOverride.Value;

            Host = new TServiceHost();
            _siloStopped = Task.Run(() => Host.Run(serviceArguments));

            //Silo is ready or failed to start
            Task.WaitAny(_siloStopped, Host.WaitForServiceStartedAsync());
            if(_siloStopped.IsFaulted)
                throw new Exception("Silo Failed to start",_siloStopped.Exception);
            if (_siloStopped.IsCompleted)
                throw new Exception("Silo Failed to start");
        }

        public override void Dispose()
        {
            Host.Stop();
            var completed = _siloStopped.Wait(60000);

            if (!completed)
                throw new TimeoutException("ServiceTester: The service failed to shutdown within the 60 second limit.");
        }
    }
}