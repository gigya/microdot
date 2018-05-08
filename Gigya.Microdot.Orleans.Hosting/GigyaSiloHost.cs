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
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Measurement;
using Gigya.Microdot.SharedLogic.Utils;
using Metrics;
using Orleans;
using Orleans.CodeGeneration;
using Orleans.Providers;
using Orleans.Runtime.Host;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class GigyaSiloHost
    {
        private readonly ITracingContext _tracingContext;
        public static IGrainFactory GrainFactory { get; private set; }
        private SiloHost Silo { get; set; }
        private Exception BootstrapException { get; set; }
        private Func<IGrainFactory, Task> AfterOrleansStartup { get; set; }
        private Func<IGrainFactory, Task> BeforeOrleansShutdown { get; set; }
        private Counter EventsDiscarded { get; }
        private ILog Log { get; }
        private OrleansConfigurationBuilder ConfigBuilder { get; }
        private HttpServiceListener HttpServiceListener { get; }
        private IEventPublisher<GrainCallEvent> EventPublisher { get; }


        public GigyaSiloHost(ILog log, OrleansConfigurationBuilder configBuilder,
                             HttpServiceListener httpServiceListener,
                             IEventPublisher<GrainCallEvent> eventPublisher,ITracingContext tracingContext)
        {
            _tracingContext = tracingContext;
            Log = log;
            ConfigBuilder = configBuilder;
            HttpServiceListener = httpServiceListener;
            EventPublisher = eventPublisher;

            if (DelegatingBootstrapProvider.OnInit != null || DelegatingBootstrapProvider.OnClose != null)
                throw new InvalidOperationException("DelegatingBootstrapProvider is already in use.");

            DelegatingBootstrapProvider.OnInit = BootstrapInit;
            DelegatingBootstrapProvider.OnClose = BootstrapClose;

            EventsDiscarded = Metric.Context("GigyaSiloHost").Counter("GrainCallEvents discarded", Unit.Items);
        }

        public void Start(Func<IGrainFactory, Task> afterOrleansStartup = null,
            Func<IGrainFactory, Task> beforeOrleansShutdown = null)
        {
            AfterOrleansStartup = afterOrleansStartup;
            BeforeOrleansShutdown = beforeOrleansShutdown;

            Log.Info(_ => _("Starting Orleans silo..."));

            Silo = new SiloHost(CurrentApplicationInfo.HostName, ConfigBuilder.ClusterConfiguration)
            {
                Type = ConfigBuilder.SiloType
            };
            Silo.InitializeOrleansSilo();


            bool siloStartedSuccessfully = Silo.StartOrleansSilo(false);

            if (siloStartedSuccessfully)
                Log.Info(_ => _("Successfully started Orleans silo", unencryptedTags: new { siloName = Silo.Name, siloType = Silo.Type }));
            else if (BootstrapException != null)
                throw new ProgrammaticException("Failed to start Orleans silo due to an exception thrown in the bootstrap method.", unencrypted: new Tags { { "siloName", Silo.Name }, { "siloType", Silo.Type.ToString() } }, innerException: BootstrapException);
            else
                throw new ProgrammaticException("Failed to start Orleans silo", unencrypted: new Tags { { "siloName", Silo.Name }, { "siloType", Silo.Type.ToString() } });
        }



        public void Stop()
        {
            HttpServiceListener.Dispose();


            try
            {
                if (Silo != null && Silo.IsStarted)
                    Silo.StopOrleansSilo();
            }
            catch (System.Net.Sockets.SocketException)
            {
                //Orleans 1.3.1 thorws this exception most of the time 
            }
            finally
            {
                try
                {
                    GrainClient.Uninitialize();
                }
                catch (Exception exc)
                {
                    Log.Warn("Exception Uninitializing grain client", exception: exc);
                }
            }

        }

        private async Task BootstrapInit(IProviderRuntime providerRuntime)
        {
            GrainTaskScheduler = TaskScheduler.Current;
            GrainFactory = providerRuntime.GrainFactory;
            providerRuntime.SetInvokeInterceptor(Interceptor);

            try
            {
                if (AfterOrleansStartup != null)
                    await AfterOrleansStartup(GrainFactory);
            }
            catch (Exception ex)
            {
                BootstrapException = ex;
                throw;
            }

            try
            {
                HttpServiceListener.Start();
            }
            catch (Exception ex)
            {
                BootstrapException = ex;
                Log.Error("Failed to start HttpServiceListener", exception: ex);
                throw;
            }
        }


        public TaskScheduler GrainTaskScheduler { get; set; }


        private async Task<object> Interceptor(MethodInfo targetMethod, InvokeMethodRequest request, IGrain target, IGrainMethodInvoker invoker)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));

            var declaringNameSpace = targetMethod.DeclaringType?.Namespace;

            // Do not intercept Orleans grains or other grains which should not be included in statistics.
            if (targetMethod.DeclaringType.GetCustomAttribute<ExcludeGrainFromStatisticsAttribute>() != null ||
               declaringNameSpace?.StartsWith("Orleans") == true)
                return await invoker.Invoke(target, request);

            RequestTimings.GetOrCreate(); // Ensure request timings is created here and not in the grain call.

            RequestTimings.Current.Request.Start();
            Exception ex = null;

            try
            {
                return await invoker.Invoke(target, request);
            }
            catch (HttpRequestException e)
            {
                ex = e;

                if (e.InnerException != null)
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                throw new EnvironmentException("[HttpRequestException] " + e.RawMessage(),
                    unencrypted: new Tags { { "originalStackTrace", e.StackTrace } });
            }
            catch (Exception e)
            {
                ex = e;

                throw;
            }
            finally
            {
                RequestTimings.Current.Request.Stop();
                var grainEvent = EventPublisher.CreateEvent();

                if (target.GetPrimaryKeyString() != null)
                {
                    grainEvent.GrainKeyString = target.GetPrimaryKeyString();
                }
                else if(target.IsPrimaryKeyBasedOnLong())
                {
                    grainEvent.GrainKeyLong = target.GetPrimaryKeyLong(out var keyExt);
                    grainEvent.GrainKeyExtention = keyExt;
                }
                else
                {
                    grainEvent.GrainKeyGuid = target.GetPrimaryKey(out var keyExt);
                    grainEvent.GrainKeyExtention = keyExt;
                }

                if (target is Grain grainTarget)
                {
                    grainEvent.SiloAddress = grainTarget.RuntimeIdentity;
                }

                grainEvent.SiloDeploymentId =  ConfigBuilder.ClusterConfiguration.Globals.DeploymentId;


                grainEvent.TargetType = targetMethod.DeclaringType?.FullName;
                grainEvent.TargetMethod = targetMethod.Name;
                grainEvent.Exception = ex;
                grainEvent.ErrCode = ex != null ? null : (int?)0;

                try
                {
                    EventPublisher.TryPublish(grainEvent);
                }
                catch (Exception)
                {
                    EventsDiscarded.Increment();
                }
            }
        }

        private async Task BootstrapClose()
        {
            if (BeforeOrleansShutdown != null)
                await BeforeOrleansShutdown(GrainFactory);
        }
    }


}