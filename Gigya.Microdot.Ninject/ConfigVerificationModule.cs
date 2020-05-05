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
using System.Diagnostics;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Logging;
using Ninject;
using Ninject.Modules;

namespace Gigya.Microdot.Ninject
{
    // ReSharper disable once ClassNeverInstantiated.Local

    /// <summary>
    /// </summary>
    public class ConfigVerificationModule : NinjectModule
    {
        /// <summary>
        /// Null ending implementation of log and event publisher to satisfy DI chain of initialization in configuration verification mode.
        /// Strictly for config verification.
        /// Should not be used in any other scenarios.
        /// Subject to change.
        /// </summary>
        private sealed class ConfigVerificationNullLogAndPublisher : LogBase, IEventPublisher
        {
            protected override Task<bool> WriteLog(TraceEventType level, LogCallSiteInfo logCallSiteInfo, string message, IDictionary<string, string> encryptedTags, IDictionary<string, string> unencryptedTags, Exception exception = null, string stackTrace = null)
            {
                return Task.FromResult(true);
            }

            public override TraceEventType? MinimumTraceLevel { get => TraceEventType.Verbose; set{} }

            private static readonly PublishingTasks PublishingTasks = new PublishingTasks
            {
                PublishEvent = Task.FromResult(true), 
                PublishAudit = Task.FromResult(true)
            };

            public PublishingTasks TryPublish(IEvent evt)
            {            
                return PublishingTasks;
            }
        }

        private readonly ILoggingModule _loggingModule = null;
        private readonly ServiceArguments _arguments;
        private readonly string _serviceName;
        private readonly Version _infraVersion = null;

        /// <summary>
        /// </summary>
        public ConfigVerificationModule (ILoggingModule loggingModule, ServiceArguments arguments, string serviceName, Version infraVersion = null)
        {
            _serviceName = serviceName;

            if(_serviceName == null)
                throw new ArgumentNullException(nameof(_serviceName));

            _infraVersion = infraVersion;
            _loggingModule = loggingModule;
            _arguments = arguments;
        }

        public override void Load()
        {
            Kernel.Load<MicrodotModule>();
            
            // Required to allow assembly provider been instantiated
            Kernel.Rebind<ServiceArguments>().ToConstant(_arguments);

            _loggingModule?.Bind(Kernel.Rebind<ILog>(), Kernel.Rebind<IEventPublisher>(),Rebind<Func<string, ILog>>());

            // Be ready that no ILog bound
            if (Kernel.TryGet<ILog>() == null)
            {
                Kernel.Rebind<ILog, IEventPublisher>().To<ConfigVerificationNullLogAndPublisher>().InSingletonScope();
            }
        }
    }
}
