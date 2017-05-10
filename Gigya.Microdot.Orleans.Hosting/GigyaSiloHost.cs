using System;
using System.Reflection;
using System.Threading.Tasks;

using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.SharedLogic;
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


        public GigyaSiloHost(ILog log, OrleansConfigurationBuilder configBuilder,
                             HttpServiceListener httpServiceListener, 
                             IEventPublisher<GrainCallEvent> eventPublisher)
        {
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

            if (Silo != null && Silo.IsStarted)
                Silo.StopOrleansSilo();

            try
            {
                GrainClient.Uninitialize();
            }
            catch (Exception exc)
            {
                Log.Warn("Exception Uninitializing grain client", exception: exc);
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
                Log.Error("Failed to start HttpServiceListener",exception:ex);                
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
            if(targetMethod.DeclaringType.GetCustomAttribute<ExcludeGrainFromStatisticsAttribute>()!=null ||
               declaringNameSpace?.StartsWith("Orleans") == true)
                 return await invoker.Invoke(target, request);

            RequestTimings.GetOrCreate(); // Ensure request timings is created here and not in the grain call.

            RequestTimings.Current.Request.Start();
            Exception ex = null;

            try
            {
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
                var grainEvent = EventPublisher.CreateEvent();
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