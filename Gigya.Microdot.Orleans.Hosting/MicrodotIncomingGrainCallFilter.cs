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
using System.Reflection;
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Orleans.Hosting
{
    //TODO create two separate filters for
    public class MicrodotIncomingGrainCallFilter : IIncomingGrainCallFilter
    {
        private readonly ILog _log;
        private readonly ClusterIdentity _clusterIdentity;
        private Counter EventsDiscarded { get; }

        private readonly IEventPublisher<GrainCallEvent> _eventPublisher;
        private readonly Func<LoadShedding> _loadSheddingConfig;

        public MicrodotIncomingGrainCallFilter(IEventPublisher<GrainCallEvent> eventPublisher,
            Func<LoadShedding> loadSheddingConfig, ILog log, ClusterIdentity clusterIdentity)
        {
            _log = log;
            _clusterIdentity = clusterIdentity;

            _eventPublisher = eventPublisher;
            _loadSheddingConfig = loadSheddingConfig;
            EventsDiscarded = Metric.Context("GigyaSiloHost").Counter("GrainCallEvents discarded", Unit.Items);
        }

        public async Task Invoke(IIncomingGrainCallContext context)
        {
            if (context.InterfaceMethod == null)
                throw new ArgumentNullException(nameof(context.InterfaceMethod));

            var declaringNameSpace = context.InterfaceMethod.DeclaringType?.Namespace;

            // Do not intercept Orleans grains or other grains which should not be included in statistics.
            if (context.InterfaceMethod.DeclaringType.GetCustomAttribute<ExcludeGrainFromStatisticsAttribute>() !=
                null || declaringNameSpace?.StartsWith("Orleans") == true)
            {
                await context.Invoke();
                return;
            }

            RequestTimings.GetOrCreate(); // Ensure request timings is created here and not in the grain call.

            RequestTimings.Current.Request.Start();
            Exception ex = null;

            try
            {
                RejectRequestIfLateOrOverloaded();
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
                PublishEvent(context, ex);
            }
        }

        private void PublishEvent(IGrainCallContext target, Exception ex)
        {
            var grainEvent = _eventPublisher.CreateEvent();

            if (target.Grain?.GetPrimaryKeyString() != null)
            {
                grainEvent.GrainKeyString = target.Grain.GetPrimaryKeyString();
            }
            else if (target.Grain.IsPrimaryKeyBasedOnLong())
            {
                grainEvent.GrainKeyLong = target.Grain.GetPrimaryKeyLong(out var keyExt);
                grainEvent.GrainKeyExtention = keyExt;
            }
            else
            {
                grainEvent.GrainKeyGuid = target.Grain.GetPrimaryKey(out var keyExt);
                grainEvent.GrainKeyExtention = keyExt;
            }

            if (target is Grain grainTarget)
            {
                grainEvent.SiloAddress = grainTarget.RuntimeIdentity;
            }

             grainEvent.SiloDeploymentId = _clusterIdentity.DeploymentId;

            grainEvent.TargetType = target.InterfaceMethod.DeclaringType?.FullName;
            grainEvent.TargetMethod = target.InterfaceMethod.Name;
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

        private void RejectRequestIfLateOrOverloaded()
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
                    }));
                else if (config.DropOrleansRequestsBySpanTime == LoadShedding.Toggle.Drop)
                    throw new EnvironmentException("Dropping Orleans request since too much time passed since the client sent it to us.", unencrypted: new Tags
                    {
                        ["clientSendTime"] = TracingContext.SpanStartTime.ToString(),
                        ["currentTime"] = now.ToString(),
                        ["maxDelayInSecs"] = config.DropOrleansRequestsOlderThanSpanTimeBy.TotalSeconds.ToString(),
                        ["actualDelayInSecs"] = (now - TracingContext.SpanStartTime.Value).TotalSeconds.ToString(),
                    });
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
                    }));
                else if (config.DropRequestsByDeathTime == LoadShedding.Toggle.Drop)
                    throw new EnvironmentException("Dropping Orleans request since the API gateway timeout passed.", unencrypted: new Tags
                    {
                        ["requestDeathTime"] = TracingContext.AbandonRequestBy.ToString(),
                        ["currentTime"] = now.ToString(),
                        ["overTimeInSecs"] = (now - TracingContext.AbandonRequestBy.Value).TotalSeconds.ToString(),
                    });
            }
        }
    }
}