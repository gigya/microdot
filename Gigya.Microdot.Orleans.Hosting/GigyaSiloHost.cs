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
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Events;
using Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.CodeGeneration;
using Orleans.Hosting;
using Metric = Metrics.Metric;


namespace Gigya.Microdot.Orleans.Hosting
{
    public interface IServiceProviderInit
    {
        IServiceProvider ConfigureServices(IServiceCollection services);
    }
    public class GigyaSiloHost
    {
        public static IGrainFactory GrainFactory { get; private set; }
        private Exception _startupTaskExceptions { get; set; }
        private Func<IGrainFactory, Task> AfterOrleansStartup { get; set; }
        private Func<IGrainFactory, Task> BeforeOrleansShutdown { get; set; }
        private Counter EventsDiscarded { get; }
        private ILog Log { get; }
        private HttpServiceListener HttpServiceListener { get; }
        private IEventPublisher<GrainCallEvent> EventPublisher { get; }
        private Func<LoadShedding> LoadSheddingConfig { get; }
        private CurrentApplicationInfo AppInfo { get; }


        public GigyaSiloHost(ILog log,
            HttpServiceListener httpServiceListener,
            IEventPublisher<GrainCallEvent> eventPublisher, Func<LoadShedding> loadSheddingConfig, CurrentApplicationInfo appInfo)

        {
            Log = log;
            HttpServiceListener = httpServiceListener;
            EventPublisher = eventPublisher;
            LoadSheddingConfig = loadSheddingConfig;
            AppInfo = appInfo;

            EventsDiscarded = Metric.Context("GigyaSiloHost").Counter("GrainCallEvents discarded", Unit.Items);

        }

        public void Start(IServiceProviderInit serviceProvider, OrleansLogProvider logProvider, OrleansConfigurationBuilder orleansConfigurationBuilder, Func<IGrainFactory, Task> afterOrleansStartup = null,
            Func<IGrainFactory, Task> beforeOrleansShutdown = null)
        {
            AfterOrleansStartup = afterOrleansStartup;
            BeforeOrleansShutdown = beforeOrleansShutdown;

            Log.Info(_ => _("Starting Orleans silo..."));

            var silo = orleansConfigurationBuilder.GetBuilder()
                .UseServiceProviderFactory(serviceProvider.ConfigureServices)
                .ConfigureLogging(op => op.AddProvider(logProvider))
                .AddStartupTask(StartupTask)
                //.AddIncomingGrainCallFilter<MicrodotIncomingGrainCallFilter>()
                //.AddOutgoingGrainCallFilter(async (o) =>
                //{
                //    TracingContext.SetUpStorage();
                //    TracingContext.SpanStartTime = DateTimeOffset.UtcNow;
                //    await o.Invoke();
                //})
                .Build();


            try
            {
                silo.StartAsync().Wait();
            }
            catch (Exception e)
            {
                throw new ProgrammaticException("Failed to start Orleans silo", unencrypted: new Tags { { "siloName", AppInfo.HostName } }, innerException: e);
            }

            if (_startupTaskExceptions != null)
                throw new ProgrammaticException("Failed to start Orleans silo due to an exception thrown in the bootstrap method.", unencrypted: new Tags { { "siloName", AppInfo.HostName } }, innerException: _startupTaskExceptions);

            Log.Info(_ => _("Successfully started Orleans silo", unencryptedTags: new { siloName = AppInfo.HostName }));
        }





        public void Stop()
        {
            HttpServiceListener.Dispose();
        }

        private async Task StartupTask(IServiceProvider serviceProvider, CancellationToken arg2)
        {
            GrainTaskScheduler = TaskScheduler.Current;
            GrainFactory = serviceProvider.GetService<IGrainFactory>();

            try
            {
                if (AfterOrleansStartup != null)
                    await AfterOrleansStartup(GrainFactory);
            }
            catch (Exception ex)
            {
                _startupTaskExceptions = ex;
                throw;
            }

            try
            {
                HttpServiceListener.Start();
            }
            catch (Exception ex)
            {
                _startupTaskExceptions = ex;
                Log.Error("Failed to start HttpServiceListener", exception: ex);
                throw;
            }
        }


        public TaskScheduler GrainTaskScheduler { get; set; }






    }
}