using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.Microdot.SharedLogic.Configurations;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Measurement;
using Metrics;
using Orleans;
using System;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting
{
    //TODO: create separate filters for concerns of logging, load shedding and so on
    public class MicrodotIncomingGrainCallFilter : IIncomingGrainCallFilter
    {
        private readonly ILog _log;
        private readonly ClusterIdentity _clusterIdentity;

        private readonly Func<GrainLoggingConfig> _grainLoggingConfig;
        private Counter EventsDiscarded { get; }

        private readonly IEventPublisher<GrainCallEvent> _eventPublisher;
        private readonly Func<LoadShedding> _loadSheddingConfig;

        public MicrodotIncomingGrainCallFilter(IEventPublisher<GrainCallEvent> eventPublisher,
            Func<LoadShedding> loadSheddingConfig, ILog log, ClusterIdentity clusterIdentity, Func<GrainLoggingConfig> grainLoggingConfig)
        {
            _log = log;
            _clusterIdentity = clusterIdentity;
            _grainLoggingConfig = grainLoggingConfig;

            _eventPublisher = eventPublisher;
            _loadSheddingConfig = loadSheddingConfig;
            EventsDiscarded = Metric.Context("GigyaSiloHost").Counter("GrainCallEvents discarded", Unit.Items);
        }

        public async Task Invoke(IIncomingGrainCallContext context)
        {

            if (context.InterfaceMethod == null || context.InterfaceMethod.DeclaringType == null)
            {
                // We observing behavior when no interface method
                // We still don't want to prevent it happen.
                await context.Invoke();
                return;
            }
            // Identify the grain type

            bool isOrleansGrain = context.InterfaceMethod.Module.Assembly.FullName?.StartsWith("Orleans") == true;
            //TODO add test that validate that we are not introducing new grain in micro dot
            bool isMicrodotGrain = context.InterfaceMethod.DeclaringType.Name == nameof(IRequestProcessingGrain);
            bool isServiceGrain = isOrleansGrain == false && isMicrodotGrain == false;

            var grainTags = GetGrainTags(context);
            // Drop the request if we're overloaded
            var loadSheddingConfig = _loadSheddingConfig();
            if (
                   (loadSheddingConfig.ApplyToMicrodotGrains && isMicrodotGrain)
                || (loadSheddingConfig.ApplyToServiceGrains && isServiceGrain)
                )
            {
                RejectRequestIfLateOrOverloaded(grainTags);
            }
            var loggingConfig = _grainLoggingConfig();

            var shouldLoad = ((loggingConfig.LogOrleansGrains && isOrleansGrain)
                              || (loggingConfig.LogMicrodotGrains && isMicrodotGrain)
                              || (loggingConfig.LogServiceGrains && isServiceGrain));
            if (shouldLoad == false)
            {
                await context.Invoke();
                return;
            }

            string callId = TracingContext.TryGetRequestID();
            uint max = (uint)Math.Round(loggingConfig.LogRatio * uint.MaxValue);
            bool shouldSkipLogging = loggingConfig.LogRatio != 1 && ( loggingConfig.LogRatio == 0 || callId == null || (uint)callId.GetHashCode() % uint.MaxValue > max);
          
            if (shouldSkipLogging)
            {
                await context.Invoke();
                return;
            }

            RequestTimings.GetOrCreate(); // Ensure request timings is created here and not in the grain call.
            RequestTimings.Current.Request.Start();
            Exception ex = null;

            try
            {
                await context.Invoke();
            }
            catch (Exception e)
            {
                ex = e;
                throw;
            }
            finally
            {
                RequestTimings.Current.Request.Stop();
                PublishEvent(ex, grainTags);
            }
        }

        private void PublishEvent(Exception ex,GrainTags grainTags)
        {
            var grainEvent = _eventPublisher.CreateEvent();

            grainEvent.GrainKeyExtention = grainTags.GrainKeyExtention;
            grainEvent.GrainKeyGuid = grainTags.GrainKeyGuid;
            grainEvent.GrainKeyLong = grainTags.GainKeyLong;
            grainEvent.GrainKeyString = grainTags.GrainKeyString;
            grainEvent.SiloAddress = grainTags.SiloAddress;
            grainEvent.SiloDeploymentId = _clusterIdentity.DeploymentId;
            grainEvent.TargetType = grainTags.TargetType;
            grainEvent.TargetMethod = grainTags.TargetMethod;
            grainEvent.Exception = ex;
            grainEvent.ErrCode = ex != null ? null : (int?)0;

            try
            {
                _eventPublisher.TryPublish(grainEvent);
            }
            catch (Exception)
            {
                EventsDiscarded.Increment();
            }
        }

        private  GrainTags GetGrainTags(IGrainCallContext target)
        {
            string grainKeyString = null;
            Guid? grainKeyGuid = null;
            long? grainKeyLong = null;
            string grainKeyExtention = null;
            string siloAddress = null;
            if (target.Grain != null && target.Grain is ISystemTarget == false)
            {
                if (target.Grain?.GetPrimaryKeyString() != null)
                {
                    grainKeyString = target.Grain.GetPrimaryKeyString();
                }
                else if (target.Grain.IsPrimaryKeyBasedOnLong())
                {
                    grainKeyLong = target.Grain.GetPrimaryKeyLong(out var keyExt);
                    grainKeyExtention = keyExt;
                }
                else
                {
                    grainKeyGuid = target.Grain.GetPrimaryKey(out var keyExt);
                    grainKeyExtention = keyExt;
                }

                if (target is Grain grainTarget)
                {
                    siloAddress = grainTarget.RuntimeIdentity;
                }
            }

            var grainType = target.Grain?.GetType().FullName ?? target.InterfaceMethod.DeclaringType?.FullName;
            var targetMethod = target.InterfaceMethod.Name;

            return new GrainTags(grainKeyString, grainKeyGuid, grainKeyLong, grainKeyExtention, siloAddress, grainType, targetMethod);
        }

        class GrainTags
        {
            public readonly string GrainKeyString;
            public readonly Guid? GrainKeyGuid;
            public readonly long? GainKeyLong;
            public readonly string GrainKeyExtention;
            public readonly string SiloAddress;
            public readonly string TargetType;
            public readonly string TargetMethod;
            public GrainTags(string grainKeyString, Guid? grainKeyGuid, long? grainKeyLong, string grainKeyExtention, string siloAddress, string targetType, string targetMethod)
            {
                GrainKeyString = grainKeyString;
                GrainKeyGuid = grainKeyGuid;
                GainKeyLong = grainKeyLong;
                GrainKeyExtention = grainKeyExtention;
                SiloAddress = siloAddress;
                TargetType = targetType;
                TargetMethod = targetMethod;
            }

        }
        private void RejectRequestIfLateOrOverloaded(GrainTags grainTags)
        {
            var config = _loadSheddingConfig();
            var now = DateTimeOffset.UtcNow;
            // Too much time passed since our direct caller made the request to us; something's causing a delay. Log or reject the request, if needed.
            if (config.DropOrleansRequestsBySpanTime != LoadShedding.Toggle.Disabled
                && TracingContext.SpanStartTime != null
                && TracingContext.SpanStartTime.Value + config.DropOrleansRequestsOlderThanSpanTimeBy < now)
            {
                if (config.DropOrleansRequestsBySpanTime == LoadShedding.Toggle.LogOnly)
                    _log.Warn(_ => _("Accepted Orleans request despite that too much time passed since the client sent it to us.", unencryptedTags: new
                    {
                        clientSendTime = TracingContext.SpanStartTime,
                        currentTime = now,
                        maxDelayInSecs = config.DropOrleansRequestsOlderThanSpanTimeBy.TotalSeconds,
                        actualDelayInSecs = (now - TracingContext.SpanStartTime.Value).TotalSeconds,
                        targetType = grainTags.TargetType,
                        targetMethod = grainTags.TargetMethod,
                        grainKeyExtention = grainTags.GrainKeyExtention,
                        grainKeyGuid = grainTags.GrainKeyGuid?.ToString(),
                        grainKeyString = grainTags.GrainKeyString,
                        siloAddress = grainTags.SiloAddress,
                        gainKeyLong = grainTags.GainKeyLong?.ToString(),

                    }));
                else if (config.DropOrleansRequestsBySpanTime == LoadShedding.Toggle.Drop)
                {
                    //Add grain  id to tags  
                    throw new EnvironmentException(
                         "Dropping Orleans request since too much time passed since the client sent it to us.",
                         unencrypted: new Tags
                         {
                             ["clientSendTime"] = TracingContext.SpanStartTime.ToString(),
                             ["currentTime"] = now.ToString(),
                             ["maxDelayInSecs"] = config.DropOrleansRequestsOlderThanSpanTimeBy.TotalSeconds.ToString(),
                             ["actualDelayInSecs"] = (now - TracingContext.SpanStartTime.Value).TotalSeconds.ToString(),
                             ["targetType"] = grainTags.TargetType,
                             ["targetMethod"] = grainTags.TargetMethod,
                             ["grainKeyExtention"] = grainTags.GrainKeyExtention,
                             ["grainKeyGuid"] = grainTags.GrainKeyGuid?.ToString(),
                             ["grainKeyString"] = grainTags.GrainKeyString,
                             ["siloAddress"] = grainTags.SiloAddress,
                             ["gainKeyLong"] = grainTags.GainKeyLong?.ToString(),
                            }); 
                }

            }

            // Too much time passed since the API gateway initially sent this request till it reached us (potentially
            // passing through other micro-services along the way). Log or reject the request, if needed.
            if (config.DropRequestsByDeathTime != LoadShedding.Toggle.Disabled
                && TracingContext.AbandonRequestBy != null
                && now > TracingContext.AbandonRequestBy.Value - config.TimeToDropBeforeDeathTime)
            {
                if (config.DropRequestsByDeathTime == LoadShedding.Toggle.LogOnly)
                    _log.Warn(_ => _("Accepted Orleans request despite exceeding the API gateway timeout.", unencryptedTags: new
                    {
                        requestDeathTime = TracingContext.AbandonRequestBy,
                        currentTime = now,
                        overTimeInSecs = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds,
                        targetType = grainTags.TargetType,
                        targetMethod = grainTags.TargetMethod,
                        grainKeyExtention = grainTags.GrainKeyExtention,
                        grainKeyGuid = grainTags.GrainKeyGuid?.ToString(),
                        grainKeyString = grainTags.GrainKeyString,
                        siloAddress = grainTags.SiloAddress,
                        gainKeyLong = grainTags.GainKeyLong?.ToString(),

                    }));
                else if (config.DropRequestsByDeathTime == LoadShedding.Toggle.Drop)
                {
                    //Add grain  id to tags  
                    throw new EnvironmentException("Dropping Orleans request since the API gateway timeout passed.", unencrypted: new Tags
                    {
                        ["requestDeathTime"] = TracingContext.AbandonRequestBy.ToString(),
                        ["currentTime"] = now.ToString(),
                        ["overTimeInSecs"] = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds.ToString(),
                        ["targetType"] = grainTags.TargetType,
                        ["targetMethod"] = grainTags.TargetMethod,
                        ["grainKeyExtention"] = grainTags.GrainKeyExtention,
                        ["grainKeyGuid"] = grainTags.GrainKeyGuid?.ToString(),
                        ["grainKeyString"] = grainTags.GrainKeyString,
                        ["siloAddress"] = grainTags.SiloAddress,
                        ["gainKeyLong"] = grainTags.GainKeyLong?.ToString(),
                    });
              
                }
            }
        }
    }
}