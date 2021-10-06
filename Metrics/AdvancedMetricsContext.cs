﻿using Metrics.Core;
using Metrics.MetricData;
using Metrics.Sampling;
using System;

namespace Metrics
{
    public interface AdvancedMetricsContext : Utils.IHideObjectMembers
    {
        /// <summary>
        /// Attach a context that has already been created (ex: by a library exposing internal metrics)
        /// </summary>
        /// <param name="contextName">name of the context to attach</param>
        /// <param name="context">Existing context instance.</param>
        /// <returns>true if the context was attached, false otherwise.</returns>
        bool AttachContext(string contextName, MetricsContext context);

        /// <summary>
        /// All metrics operations will be NO-OP.
        /// This is useful for measuring the impact of the metrics library on the application.
        /// If you think the Metrics library is causing issues, this will disable all Metrics operations.
        /// </summary>
        void CompletelyDisableMetrics();

        /// <summary>
        /// Clear all collected data for all the metrics in this context
        /// </summary>
        void ResetMetricsValues();

        /// <summary>
        /// Event fired when the context is disposed or shutdown or the CompletelyDisableMetrics is called.
        /// </summary>
        event EventHandler ContextShuttingDown;

        /// <summary>
        /// Event fired when the context CompletelyDisableMetrics is called.
        /// </summary>
        event EventHandler ContextDisabled;

        /// <summary>
        /// Register a custom Gauge instance.
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all counters in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="valueProvider">Function used to build a custom instance.</param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        void Gauge(string name, Func<MetricValueProvider<double>> valueProvider, Unit unit, MetricTags tags = default(MetricTags));

        /// <summary>
        /// Register a custom Counter instance
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all counters in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="builder">Function used to build a custom instance.</param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        /// <returns>Reference to the metric</returns>
        Counter Counter<T>(string name, Unit unit, Func<T> builder, MetricTags tags = default(MetricTags))
            where T : CounterImplementation;

        /// <summary>
        /// Register a custom Meter instance.
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all meters in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="builder">Function used to build a custom instance.</param>
        /// <param name="rateUnit">Time unit for rates reporting. Defaults to Second ( occurrences / second ).</param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        /// <returns>Reference to the metric</returns>
        Meter Meter<T>(string name, Unit unit, Func<T> builder, TimeUnit rateUnit = TimeUnit.Seconds, MetricTags tags = default(MetricTags))
            where T : MeterImplementation;

        /// <summary>
        /// Register a custom Histogram instance
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all histograms in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="builder">Function used to build a custom instance.</param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        /// <returns>Reference to the metric</returns>
        Histogram Histogram<T>(string name, Unit unit, Func<T> builder, MetricTags tags = default(MetricTags))
            where T : HistogramImplementation;

        /// <summary>
        /// Register a Histogram metric with a custom Reservoir instance
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all histograms in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="builder">Function used to build a custom reservoir instance.</param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        /// <returns>Reference to the metric</returns>
        Histogram Histogram(string name, Unit unit, Func<Reservoir> builder, MetricTags tags = default(MetricTags));

        /// <summary>
        /// Register a custom Timer implementation.
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all timers in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="builder">Function used to build a custom instance.</param>
        /// <param name="rateUnit">Time unit for rates reporting. Defaults to Second ( occurrences / second ).</param>
        /// <param name="durationUnit">Time unit for reporting durations. Defaults to Milliseconds. </param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        /// <returns>Reference to the metric</returns>
        Timer Timer<T>(string name, Unit unit, Func<T> builder, TimeUnit rateUnit = TimeUnit.Seconds, TimeUnit durationUnit = TimeUnit.Milliseconds, MetricTags tags = default(MetricTags))
            where T : TimerImplementation;

        /// <summary>
        /// Register a Timer metric with a custom Histogram implementation.
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all timers in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="builder">Function used to build a custom histogram instance.</param>
        /// <param name="rateUnit">Time unit for rates reporting. Defaults to Second ( occurrences / second ).</param>
        /// <param name="durationUnit">Time unit for reporting durations. Defaults to Milliseconds. </param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        /// <returns>Reference to the metric</returns>
        Timer Timer(string name, Unit unit, Func<HistogramImplementation> builder, TimeUnit rateUnit = TimeUnit.Seconds, TimeUnit durationUnit = TimeUnit.Milliseconds, MetricTags tags = default(MetricTags));

        /// <summary>
        /// Register a Timer metric with a custom Reservoir implementation for the histogram.
        /// </summary>
        /// <param name="name">Name of the metric. Must be unique across all timers in this context.</param>
        /// <param name="unit">Description of what the is being measured ( Unit.Requests , Unit.Items etc ) .</param>
        /// <param name="builder">Function used to build a custom reservoir instance.</param>
        /// <param name="rateUnit">Time unit for rates reporting. Defaults to Second ( occurrences / second ).</param>
        /// <param name="durationUnit">Time unit for reporting durations. Defaults to Milliseconds. </param>
        /// <param name="tags">Optional set of tags that can be associated with the metric.</param>
        /// <returns>Reference to the metric</returns>
        Timer Timer(string name, Unit unit, Func<Reservoir> builder, TimeUnit rateUnit = TimeUnit.Seconds, TimeUnit durationUnit = TimeUnit.Milliseconds, MetricTags tags = default(MetricTags));

        /// <summary>
        /// Replace the DefaultMetricsBuilder used in this context.
        /// </summary>
        /// <param name="metricsBuilder">The custom metrics builder.</param>
        void WithCustomMetricsBuilder(MetricsBuilder metricsBuilder);
    }
}
