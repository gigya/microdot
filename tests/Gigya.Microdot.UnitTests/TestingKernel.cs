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
using System.Net;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.Fakes.KernelUtils;
using Gigya.Microdot.Hosting;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.Ninject.SystemInitializer;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Monitor;
using Ninject;
using NSubstitute;

namespace Gigya.Microdot.Testing.Shared
{

    public class TestingKernel<T> : StandardKernel where T : ILog, new()
    {
        public const string APPNAME = "InfraTests";

        /// <summary>
        /// Construction of TestingKernel should always be ended by SystemInitializer.Init(), which performs IConfigObjects rebinding.
        /// Don't pass any "IConfigObjects actions" in additionalBinfings parameter.
        /// </summary>
        /// <param name="additionalBindings"></param>
        /// <param name="mockConfig"></param>
        public TestingKernel(Action<IKernel> additionalBindings = null, Dictionary<string, string> mockConfig = null)
        {
            ServicePointManager.DefaultConnectionLimit = 200;
               Bind<CurrentApplicationInfo>().ToConstant(new CurrentApplicationInfo(APPNAME)).InSingletonScope();
            this.Load<MicrodotModule>();
            Rebind<IEventPublisher>().To<NullEventPublisher>();
            Rebind<ILog>().To<T>().InSingletonScope();
            Rebind<IDiscovery>().To<AlwaysLocalhostDiscovery>().InSingletonScope();
            Rebind<IDiscoverySourceLoader>().To<AlwaysLocalHost>().InSingletonScope();
            var locationsParserMock = Substitute.For<IConfigurationLocationsParser>();
            locationsParserMock.ConfigFileDeclarations.Returns(Enumerable.Empty<ConfigFileDeclaration>().ToArray());
            Rebind<IConfigurationLocationsParser>().ToConstant(locationsParserMock);
            Rebind<IMetricsInitializer>().To<MetricsInitializerFake>().InSingletonScope();

            Rebind<IHealthMonitor>().To<FakeHealthMonitor>().InSingletonScope();
            this.WithNoCrashHandler();
            additionalBindings?.Invoke(this);

            Rebind<IConfigurationDataWatcher, ManualConfigurationEvents>()
                .To<ManualConfigurationEvents>()
                .InSingletonScope();

            Rebind<IConfigItemsSource, OverridableConfigItems>()
                .To<OverridableConfigItems>()
                .InSingletonScope()
                .WithConstructorArgument("data", mockConfig ?? new Dictionary<string, string>());

            this.Get<SystemInitializer>().Init();
        }


    }
}