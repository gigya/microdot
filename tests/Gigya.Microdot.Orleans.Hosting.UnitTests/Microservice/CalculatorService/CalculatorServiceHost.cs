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

using System.Collections.Generic;
using System.Diagnostics;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Orleans.Ninject.Host;
using Ninject;
using Ninject.Syntax;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
{
    public class FakesLoggersModules : ILoggingModule
    {
        private readonly bool _useHttpLog;

        public FakesLoggersModules(bool useHttpLog)
        {
            _useHttpLog = useHttpLog;
        }

        public void Bind(IBindingToSyntax<ILog> logBinding, IBindingToSyntax<IEventPublisher> eventPublisherBinding)
        {
            if (_useHttpLog)
                logBinding.To<HttpLog>();
            else
                logBinding.To<ConsoleLog>();

            eventPublisherBinding.To<SpyEventPublisher>().InSingletonScope();
        }
    }

    public class CalculatorServiceHost : MicrodotOrleansServiceHost
    {
        private ILoggingModule LoggingModule { get; }

        public CalculatorServiceHost() : this( true)
        { }


        public CalculatorServiceHost( bool useHttpLog)
        {
            LoggingModule = new FakesLoggersModules(useHttpLog);
        }


        protected override string ServiceName => "TestService";


        public override ILoggingModule GetLoggingModule()
        {
            return LoggingModule;
        }

        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            kernel.Rebind<ServiceValidator>().To<MockServiceValidator>().InSingletonScope();


            kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>();
            kernel.Rebind<ILog>().ToConstant(new HttpLog(TraceEventType.Warning));
        }

        public class MockServiceValidator : ServiceValidator
        {

            public MockServiceValidator()
                : base(new List<IValidator>().ToArray())
            {

            }
        }
    }



}