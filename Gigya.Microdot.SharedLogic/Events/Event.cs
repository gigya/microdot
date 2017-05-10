using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.SharedLogic.Logging;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.SharedLogic.Events
{
    /// <summary>The base class of events. Contains common fields that are present in all events, such as the time, reqID,
    /// publishing server, endpoint, status, message, details and exception.</summary>
    /// <remarks>
    /// !!!  READ THIS BEFORE EDITING THIS OR DERIVED CLASSES !!!
    /// 
    ///   * Each field/property must be decorated with <see cref="FlumeFieldAttribute"/> to be sent to flume.
    /// 
    ///   * Use get-only properties for data fetched from context, that way IntelliSense won't suggest filling them when
    ///     creating a new event instance.
    /// 
    /// </remarks>
    public class Event : IEvent
    {
        public IEnvironmentVariableProvider EnvironmentVariableProvider { get; set; }

        public IEventConfiguration Configuration { get; set; }

        /// <summary>The type of the event, for flume. Overridden by derived classes.</summary>
        [FlumeField(EventConsts.type)]
        public virtual string FlumeType => EventConsts.BaseEventType;

        /// <summary>Whether this event should be written to the audit log as well. Overridden by derived classes.</summary>
        public virtual bool ShouldAudit { get { return false; } set { } }

        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>A unique, random ID coming from Gator</summary>    
        [FlumeField(EventConsts.callID)]
        public string RequestId { get; set; } = TracingContext.TryGetRequestID();

        /// <summary>A unique, random ID coming from Gator</summary>    
        [FlumeField(EventConsts.spanID)]
        public string SpanId { get; set; } = TracingContext.TryGetSpanID();

        /// <summary>A unique, random ID coming from Gator</summary>    
        [FlumeField(EventConsts.parentSpanID)]
        public string ParentSpanId { get; set; } = TracingContext.TryGetParentSpanID();

        //============ PUBLISHER INFO ===============

        /// <summary>The name of the reporting system (comments/socialize/hades/mongo etc)</summary>
        [FlumeField(EventConsts.srvSystem, OmitFromAudit = true)]
        public string ServiceName { get; } = CurrentApplicationInfo.Name;

        /// <summary>The name of the instance of the reporting system</summary>
        [FlumeField(EventConsts.srvSystemInstance, OmitFromAudit = true)]
        public string ServiceInstanceName { get; } = CurrentApplicationInfo.InstanceName == CurrentApplicationInfo.DEFAULT_INSTANCE_NAME ? null : CurrentApplicationInfo.InstanceName;

        [FlumeField(EventConsts.srvVersion, OmitFromAudit = true)]
        public string ServiceVersion => CurrentApplicationInfo.Version.ToString(4);

        [FlumeField(EventConsts.infrVersion, OmitFromAudit = true)]
        public string InfraVersion => CurrentApplicationInfo.InfraVersion.ToString(4);

        /// <summary>The value of the %ENV% environment variable. </summary>
        [FlumeField(EventConsts.runtimeENV, OmitFromAudit = true)]
        public string RuntimeENV => EnvironmentVariableProvider.DeploymentEnvironment;

        /// <summary>The value of the %DC% environment variable. .</summary>
        [FlumeField(EventConsts.runtimeDC, OmitFromAudit = true)]
        public string RuntimeDC => EnvironmentVariableProvider.DataCenter;

        ///// <summary>The hostname of the server making the report</summary>    
        [FlumeField(EventConsts.runtimeHost)]
        public string HostName => CurrentApplicationInfo.HostName;

        //============ MESSAGE ===============
        public int? ErrCode { get; set; }

        /// <summary>Returns the explicitly-set <see cref="ErrCode"/>, or an error code deduced from the
        /// <see cref="Exception"/>, or null if neither was set.</summary>
        [FlumeField(EventConsts.errCode)]
        public int? ErrCode_ => ErrCode
                                ?? ((Exception as RequestException)?.ErrorCode ?? (Exception != null ? 500001 //General Server Error
                                        : (int?)null));

        /// <summary>A short summary of the log event</summary>
        [FlumeField(EventConsts.message)]
        public string Message { get; set; }

        /// <summary>A detailed message, when relevant</summary>
        [FlumeField(EventConsts.details)]
        public string Details { get; set; }

        /// <summary>
        /// Exception to publish as individual fields
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>If an exception occured, the exception message.</summary>
        [FlumeField(EventConsts.exMessage)]
        internal string ExceptionMessage => Exception?.RawMessage();

        /// <summary> Used for grouping on the exception message in Kibana</summary>
        [FlumeField(EventConsts.exOneWordMessage)]
        public string ExceptionOneWordMessage => Exception?.RawMessage().Replace(' ', '_');

        /// <summary>If an exception occured and it contained inner exceptions, output a newline-separated list of messages,
        /// from outer-most to inner-most.</summary>
        [FlumeField(EventConsts.exInnerMessages)]
        public string ExceptionInnerMessages
        {
            get
            {
                if (Exception?.InnerException == null)
                    return null;

                var sb = new StringBuilder(Exception.InnerException.RawMessage());
                var innerEx = Exception.InnerException.InnerException;

                while (innerEx != null)
                {
                    sb.Append("\n\n");
                    sb.AppendLine(innerEx.RawMessage());
                    innerEx = innerEx.InnerException;
                }

                return sb.ToString();
            }
        }

        /// <summary>If an exception occured, the exception stack trace.</summary>
        [FlumeField(EventConsts.exStackTrace)]
        public string ExceptionStackTrace
        {
            get
            {
                if (ShouldExcludeStackTraceForFlume || Exception == null)
                    return null;

                return Exception.StackTrace ?? Exception.InnerException?.StackTrace;
            }
        }

        /// <summary>The .Net type (full class name) of the exception.</summary>
        [FlumeField(EventConsts.exType)]
        public string ExceptionType => Exception?.GetType().FullName;

        /// <summary>Developer-provided details in key-value pairs form. Primitive types (int, float) will be indexed
        /// and be range-searchable.</summary>
        public Dictionary<string, object> UnencryptedTags { get; set; }

        /// <summary>Developer-provided details in key-value pairs form.</summary>
        public Dictionary<string, object> EncryptedTags { get; set; }

        [FlumeField(EventConsts.tags, AppendTypeSuffix = true)]
        public IEnumerable<KeyValuePair<string, object>> UnifiedUnencryptedTags =>
            Exception.GetUnencryptedTags().Concat(UnencryptedTags ?? Enumerable.Empty<KeyValuePair<string, object>>());

        [FlumeField(EventConsts.tags, Encrypt = true)]
        public IEnumerable<KeyValuePair<string, object>> UnifiedEncryptedTags =>
            Exception.GetEncryptedTagsAndExtendedProperties().Concat(EncryptedTags ?? Enumerable.Empty<KeyValuePair<string, object>>());

        /// <summary>The site ID, if applicable.</summary>
        [FlumeField(EventConsts.siteID)]
        public virtual ulong? SiteID { get; set; } = null;

        [FlumeField(EventConsts.apikey)]
        public virtual string ApiKey { get; set; } = null;

        [FlumeField(EventConsts.partnerID)]
        public virtual uint? PartnerID { get; set; } = null;

        /// <summary>Whether exception stack traces should be excluded from Flume. Note: can be overridden by derived classes.</summary>                
        public virtual bool ShouldExcludeStackTraceForFlume => Configuration.ExcludeStackTraceRule?.IsMatch(ErrCode_.ToString()) == true;


    }
}