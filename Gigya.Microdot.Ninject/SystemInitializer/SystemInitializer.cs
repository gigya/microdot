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
using System.Net;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.SharedLogic.Measurement.Workload;
using Ninject;

namespace Gigya.Microdot.Ninject.SystemInitializer
{
    public class SystemInitializer : SystemInitializerBase
    {
        private ISourceBlock<ServicePointManagerDefaultConfig> _configSource;
        private IWorkloadMetrics _workloadMetrics;

        public SystemInitializer(IKernel kernel) : base(kernel)
        {
        }

        protected override void SetDefaultTCPHTTPSettings()
        {
            _configSource = _kernel.Get<ISourceBlock<ServicePointManagerDefaultConfig>>();
            _configSource.LinkTo(new ActionBlock<ServicePointManagerDefaultConfig>(cnf => SetServicePointManagerDefaultValues(cnf)));

            ServicePointManagerDefaultConfig config = _kernel.Get<Func<ServicePointManagerDefaultConfig>>()();
            SetServicePointManagerDefaultValues(config);
        }

        protected override void InitWorkloadMetrics()
        {
            _workloadMetrics = _kernel.Get<IWorkloadMetrics>();
            _workloadMetrics.Init();
        }

        private void SetServicePointManagerDefaultValues(ServicePointManagerDefaultConfig config)
        {
            ServicePointManager.DefaultConnectionLimit = config.DefaultConnectionLimit;
            ServicePointManager.UseNagleAlgorithm = config.UseNagleAlgorithm;
            ServicePointManager.Expect100Continue = config.Expect100Continue;
        }

        public override void Dispose()
        {
            _configSource.Complete();
            _workloadMetrics?.Dispose();
        }
    }
}
