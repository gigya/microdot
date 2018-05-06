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
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Measurement;
using Metrics;
using Orleans;
using Orleans.CodeGeneration;
using Orleans.Providers;
using Orleans.Runtime.Host;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class GigyaSiloHost
    {
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
        private Func<LoadShedding> LoadSheddingConfig { get; }


        public GigyaSiloHost(ILog log, OrleansConfigurationBuilder configBuilder, HttpServiceListener httpServiceListener,
                             IEventPublisher<GrainCallEvent> eventPublisher, Func<LoadShedding> loadSheddingConfig)
        {
            Log = log;
            ConfigBuilder = configBuilder;
            HttpServiceListener = httpServiceListener;
            EventPublisher = eventPublisher;
            LoadSheddingConfig = loadSheddingConfig;

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
            providerRuntime.SetInvokeInterceptor(IncomingCallInterceptor);
            GrainClient.ClientInvokeCallback = OutgoingCallInterceptor;

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


        private void OutgoingCallInterceptor(InvokeMethodRequest request, IGrain target)
        {
            TracingContext.SetUpStorage();
            TracingContext.SpanStartTime = DateTime.UtcNow;
        }


        private async Task<object> IncomingCallInterceptor(MethodInfo targetMethod, InvokeMethodRequest request, IGrain target, IGrainMethodInvoker invoker)
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
                RejectRequestIfLateOrOverloaded();
                return await invoker.Invoke(target, request);
            }
            catch (Exception e)
            {
                ex = e;
                throw;
            }
            finally
            {
                RequestTimings.Current.Request.Stop();
                PublishEvent(targetMethod, target, ex);
            }
        }


        private void RejectRequestIfLateOrOverloaded()
        {
            var config = LoadSheddingConfig();
            var now = DateTimeOffset.UtcNow;

            // Too much time passed since our direct caller made the request to us; something's causing a delay. Log or reject the request, if needed.
            if (   config.DropOrleansRequestsBySpanTime != LoadShedding.Toggle.No
                && TracingContext.SpanStartTime != null
                && TracingContext.SpanStartTime.Value + config.DropOrleansRequestsOlderThanSpanTimeBy < now)
            {

                if (config.DropOrleansRequestsBySpanTime == LoadShedding.Toggle.LogOnly)
                    Log.Warn(_ => _("Accepted Orleans request despite that too much time passed since the client sent it to us.", unencryptedTags: new {
                        clientSendTime    = TracingContext.SpanStartTime,
                        currentTime       = now,
                        maxDelayInSecs    = config.DropOrleansRequestsOlderThanSpanTimeBy.TotalSeconds,
                        actualDelayInSecs = (now - TracingContext.SpanStartTime.Value).TotalSeconds,
                    }));

                else if (config.DropOrleansRequestsBySpanTime == LoadShedding.Toggle.Drop)
                    throw new EnvironmentException("Dropping Orleans request since too much time passed since the client sent it to us.", unencrypted: new Tags {
                        ["clientSendTime"]    = TracingContext.SpanStartTime.ToString(),
                        ["currentTime"]       = now.ToString(),
                        ["maxDelayInSecs"]    = config.DropOrleansRequestsOlderThanSpanTimeBy.TotalSeconds.ToString(),
                        ["actualDelayInSecs"] = (now - TracingContext.SpanStartTime.Value).TotalSeconds.ToString(),
                    });
            }

            // Too much time passed since the API gateway initially sent this request till it reached us (potentially
            // passing through other micro-services along the way). Log or reject the request, if needed.
            if (   config.DropRequestsByDeathTime != LoadShedding.Toggle.No
                && TracingContext.AbandonRequestBy != null
                && TracingContext.AbandonRequestBy.Value < now)
            {
                if (config.DropRequestsByDeathTime == LoadShedding.Toggle.LogOnly)
                    Log.Warn(_ => _("Accepted Orleans request despite exceeding the API gateway timeout.", unencryptedTags: new {
                        requestDeathTime = TracingContext.AbandonRequestBy,
                        currentTime      = now,
                        overTimeInSecs   = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds,
                    }));

                else if (config.DropRequestsByDeathTime == LoadShedding.Toggle.Drop)
                    throw new EnvironmentException("Dropping Orleans request since the API gateway timeout passed.", unencrypted: new Tags {
                        ["requestDeathTime"] = TracingContext.AbandonRequestBy.ToString(),
                        ["currentTime"]      = now.ToString(),
                        ["overTimeInSecs"]   = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds.ToString(),
                    });
            }
        }


        private void PublishEvent(MethodInfo targetMethod, IGrain target, Exception ex)
        {
            var grainEvent = EventPublisher.CreateEvent();

            if (target.GetPrimaryKeyString() != null)
            {
                grainEvent.GrainKeyString = target.GetPrimaryKeyString();
            }
            else if (target.IsPrimaryKeyBasedOnLong())
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

            grainEvent.SiloDeploymentId = ConfigBuilder.ClusterConfiguration.Globals.DeploymentId;


            grainEvent.TargetType = targetMethod.DeclaringType?.FullName;
            grainEvent.TargetMethod = targetMethod.Name;
            grainEvent.Exception = ex;
            grainEvent.ErrCode = ex != null ? null : (int?) 0;

            try
            {
                EventPublisher.TryPublish(grainEvent);
            }
            catch (Exception)
            {
                EventsDiscarded.Increment();
            }
        }


        private async Task BootstrapClose()
        {
            if (BeforeOrleansShutdown != null)
                await BeforeOrleansShutdown(GrainFactory);
        }
    }


}