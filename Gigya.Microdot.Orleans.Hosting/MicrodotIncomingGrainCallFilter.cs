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
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting
{
    //TODO: create separate filters for concerns of logging, load shedding and so on
    [SuppressMessage("ReSharper", "SpecifyACultureInStringConversionExplicitly")]
    public class MicrodotIncomingGrainCallFilter : IIncomingGrainCallFilter
    {
        private readonly ILog _log;
        private readonly ClusterIdentity _clusterIdentity;

        private readonly Func<GrainLoggingConfig> _grainLoggingConfig;
        private Meter DropEvent { get; }

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

            DropEvent = Metric.Context("Silo").Meter("LoadShedding Drop Event", Unit.Items);
        }

        public async Task Invoke(IIncomingGrainCallContext context)
        {
            bool isOrleansGrain = context.InterfaceMethod == null || context.InterfaceMethod.DeclaringType == null || context.InterfaceMethod.Module.Assembly.FullName.StartsWith("Orleans");
            //TODO add test that validate that we are not introducing new grain in micro dot
            bool isMicrodotGrain = isOrleansGrain == false && context.InterfaceMethod.DeclaringType.Name == nameof(IRequestProcessingGrain);
            bool isServiceGrain = isOrleansGrain == false && isMicrodotGrain == false;

            var grainTags = new Lazy<GrainTags>(() => new GrainTags(context));
            // Drop the request if we're overloaded
            var loadSheddingConfig = _loadSheddingConfig();
            if (
                   (loadSheddingConfig.ApplyToMicrodotGrains && isMicrodotGrain)
                || (loadSheddingConfig.ApplyToServiceGrains && isServiceGrain)
                )
            {
                //Can brake the flow by throwing Overloaded
                RejectRequestIfLateOrOverloaded(grainTags);
            }
            var loggingConfig = _grainLoggingConfig();

            bool shouldLog = (loggingConfig.LogOrleansGrains && isOrleansGrain)
                                   || (loggingConfig.LogMicrodotGrains && isMicrodotGrain)
                                   || (loggingConfig.LogServiceGrains && isServiceGrain);

            shouldLog = shouldLog && !ShouldSkipLoggingUnderRatio(loggingConfig, TracingContext.TryGetRequestID());
            GrainCallEvent grainEvent = null;

            if (shouldLog)
            {
                RequestTimings.GetOrCreate(); // Ensure request timings is created here and not in the grain call.
                RequestTimings.Current.Request.Start();
                grainEvent = _eventPublisher.CreateEvent();

                grainEvent.ParentSpanId = TracingContext.TryGetParentSpanID();
                grainEvent.SpanId = Guid.NewGuid().ToString("N");
                TracingContext.SetParentSpan(grainEvent.SpanId);
            }

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
                if (shouldLog)
                {
                    RequestTimings.Current.Request.Stop();
                    PublishEvent(ex, grainTags, grainEvent);
                }
            }
        }

        private static bool ShouldSkipLoggingUnderRatio(GrainLoggingConfig loggingConfig, string callId = null)
        {
            if (loggingConfig.LogRatio == 1)
                return false;

            uint max = (uint)Math.Round(loggingConfig.LogRatio * uint.MaxValue);
            bool shouldSkipLogging = (loggingConfig.LogRatio == 0 || callId == null ||
                                      (uint)callId.GetHashCode() % uint.MaxValue > max);
            return shouldSkipLogging;
        }

        private void PublishEvent(Exception ex, Lazy<GrainTags> grainTags, GrainCallEvent grainEvent)
        {
            grainEvent.GrainID = grainTags.Value.GrainId;
            grainEvent.SiloAddress = grainTags.Value.SiloAddress;
            grainEvent.SiloDeploymentId = _clusterIdentity.DeploymentId;
            grainEvent.TargetType = grainTags.Value.TargetType;
            grainEvent.TargetMethod = grainTags.Value.TargetMethod;
            grainEvent.Exception = ex;
            grainEvent.ErrCode = ex != null ? null : (int?)0;
            _eventPublisher.TryPublish(grainEvent);
        }

        class GrainTags
        {
            public readonly string GrainId;
            public readonly string SiloAddress;
            public readonly string TargetType;
            public readonly string TargetMethod;

            public GrainTags(IGrainCallContext target)
            {
                if (target.Grain != null && target.Grain is ISystemTarget == false)
                {
                    if (target.Grain.GetPrimaryKeyString() != null)
                    {
                        GrainId = target.Grain.GetPrimaryKeyString();
                    }
                    else if (target.Grain.IsPrimaryKeyBasedOnLong())
                    {
                        GrainId = target.Grain.GetPrimaryKeyLong(out var keyExt).ToString();
                        GrainId = (string.IsNullOrEmpty(keyExt) ? "" : keyExt + "/") + GrainId;
                    }
                    else
                    {
                        GrainId = target.Grain.GetPrimaryKey(out var keyExt).ToString();
                        GrainId = (string.IsNullOrEmpty(keyExt) ? "" : keyExt + "/") + GrainId;
                    }

                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (target is Grain grainTarget)
                    {
                        SiloAddress = grainTarget.RuntimeIdentity;
                    }
                }

                TargetType = target.Grain?.GetType().FullName ?? target.InterfaceMethod?.DeclaringType?.FullName;
                TargetMethod = target.InterfaceMethod?.Name;
            }
        }
        private void RejectRequestIfLateOrOverloaded(Lazy<GrainTags> grainTags)
        {
            var config = _loadSheddingConfig();
            var now = DateTimeOffset.UtcNow;
            // Too much time passed since our direct caller made the request to us; something's causing a delay. Log or reject the request, if needed.
            if (config.DropOrleansRequestsBySpanTime != LoadShedding.Toggle.Disabled
                && TracingContext.SpanStartTime != null
                && TracingContext.SpanStartTime.Value + config.DropOrleansRequestsOlderThanSpanTimeBy < now)
            {

                var totalMilliseconds = config.DropOrleansRequestsOlderThanSpanTimeBy.TotalMilliseconds.ToString();
                var actualDelayInSecs = (now - TracingContext.SpanStartTime.Value).TotalSeconds.ToString();
                if (config.DropOrleansRequestsBySpanTime == LoadShedding.Toggle.LogOnly)

                {
                    _log.Warn(_ =>
                        _("Accepted Orleans request despite that too much time passed since the client sent it to us."
                            , unencryptedTags: CreateExceptionTags(grainTags, now, nameof(config.DropOrleansRequestsOlderThanSpanTimeBy), totalMilliseconds, actualDelayInSecs)));
                }
                else if (config.DropOrleansRequestsBySpanTime == LoadShedding.Toggle.Drop)
                {
                    DropEvent.Mark();
                    throw new EnvironmentException(
                        "Dropping Orleans request since too much time passed since the client sent it to us.",
                        unencrypted: CreateExceptionTags(grainTags, now, nameof(config.DropOrleansRequestsOlderThanSpanTimeBy), totalMilliseconds, actualDelayInSecs));
                }
            }

            // Too much time passed since the API gateway initially sent this request till it reached us (potentially
            // passing through other micro-services along the way). Log or reject the request, if needed.
            if (config.DropRequestsByDeathTime != LoadShedding.Toggle.Disabled
                && TracingContext.AbandonRequestBy != null
                && now > TracingContext.AbandonRequestBy.Value - config.TimeToDropBeforeDeathTime)
            {
                var totalMilliseconds = config.TimeToDropBeforeDeathTime.TotalMilliseconds;
                var actualDelayInSecs = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds;

                if (config.DropRequestsByDeathTime == LoadShedding.Toggle.LogOnly)
                    _log.Warn(_ => _("Accepted Orleans request despite exceeding the API gateway timeout."
                        , unencryptedTags: CreateTags(grainTags, now, nameof(config.TimeToDropBeforeDeathTime), totalMilliseconds, actualDelayInSecs)));
                else if (config.DropRequestsByDeathTime == LoadShedding.Toggle.Drop)
                {
                    DropEvent.Mark();
                    //Add grain  id to tags  
                    throw new EnvironmentException("Dropping Orleans request since the API gateway timeout passed.",
                        unencrypted: CreateExceptionTags(grainTags, now, nameof(config.TimeToDropBeforeDeathTime), totalMilliseconds.ToString(), actualDelayInSecs.ToString()));

                }
            }


            object CreateTags(Lazy<GrainTags> grainTagsLazy, DateTimeOffset nowOffset, string dropConfigName, double dropConfigValue, double actualDelayInSecs)
            {
                return new
                {
                    currentTime = nowOffset,
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    dropConfigName = dropConfigName,
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    dropConfigValue = dropConfigValue,
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    actualDelayInSecs = actualDelayInSecs,
                    targetType = grainTagsLazy.Value.TargetType,
                    targetMethod = grainTagsLazy.Value.TargetMethod,
                    grainID = grainTagsLazy.Value.GrainId,
                    siloAddress = grainTagsLazy.Value.SiloAddress,
                };
            }

            Tags CreateExceptionTags(Lazy<GrainTags> grainTagsLazy, DateTimeOffset nowOffset, string dropConfigName, string dropConfigValue, string actualDelayInSecs)
            {
                return new Tags
                {
                    ["currentTime"] = nowOffset.ToString(),
                    ["dropConfigName"] = dropConfigName,
                    ["dropConfigValue"] = dropConfigValue,
                    ["actualDelayInSecs"] = actualDelayInSecs,
                    ["targetType"] = grainTagsLazy.Value.TargetType,
                    ["targetMethod"] = grainTagsLazy.Value.TargetMethod,
                    ["grainID"] = grainTagsLazy.Value.GrainId,
                    ["siloAddress"] = grainTagsLazy.Value.SiloAddress,
                };
            }
        }
    }
}