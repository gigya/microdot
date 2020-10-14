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
using System.Text;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Logging;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.SharedLogic.Events
{
    /// <summary>The base class of events. Contains common fields that are present in all events, such as the time, reqID,
    /// publishing server, endpoint, status, message, details and exception.</summary>
    /// <remarks>
    /// !!!  READ THIS BEFORE EDITING THIS OR DERIVED CLASSES !!!
    /// 
    ///   * Each field/property must be decorated with <see cref="EventFieldAttribute"/> to be published.
    /// 
    ///   * Use get-only properties for data fetched from context, that way IntelliSense won't suggest filling them when
    ///     creating a new event instance.
    /// 
    /// </remarks>
    public class Event : IEvent
    {
        public IEnvironment Environment { get; set; }
        public IStackTraceEnhancer StackTraceEnhancer { get; set; }

        public EventConfiguration Configuration { get; set; }

        /// <summary>The type of the event, for publishing. Overridden by derived classes.</summary>
        [EventField(EventConsts.type)]
        public virtual string EventType => EventConsts.BaseEventType;

        /// <summary>Whether this event should be written to the audit log as well. Overridden by derived classes.</summary>
        public virtual bool ShouldAudit { get { return false; } set { } }

        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>A unique, random ID coming from Gator</summary>    
        [EventField(EventConsts.callID)]
        public string RequestId { get; set; } = TracingContext.TryGetRequestID();

        /// <summary>A unique, random ID coming from Gator</summary>    
        [EventField(EventConsts.spanID)]
        public string SpanId { get; set; }

        /// <summary>A unique, random ID coming from Gator</summary>    
        [EventField(EventConsts.parentSpanID)]
        public string ParentSpanId { get; set; } = TracingContext.TryGetParentSpanID();

        [EventField(EventConsts.unknownTracingData, Encrypt = true)]
        public Dictionary<string, object> UnknownTracingData { get; set; } = TracingContext.AdditionalProperties;


        //============ PUBLISHER INFO ===============

        /// <summary>The name of the reporting system (comments/socialize/hades/mongo etc)</summary>
        [EventField(EventConsts.srvSystem, OmitFromAudit = true)]
        public string ServiceName { get; set;} // Publisher populated from CurrentApplicationInfo;

        /// <summary>The name of the instance of the reporting system</summary>
        [EventField(EventConsts.srvSystemInstance, OmitFromAudit = true)]
        public string ServiceInstanceName { get; set;} // Publisher populated from CurrentApplicationInfo;

        [EventField(EventConsts.srvVersion, OmitFromAudit = true)]
        public string ServiceVersion  { get; set;} // Publisher populated from CurrentApplicationInfo;

        [EventField(EventConsts.infrVersion, OmitFromAudit = true)]
        public string InfraVersion  { get; set;} // Publisher populated from CurrentApplicationInfo;

        ///// <summary>The hostname of the server making the report</summary>    
        [EventField(EventConsts.runtimeHost)]
        public string HostName  { get; set; } = CurrentApplicationInfo.HostName;

        /// <summary>The value of the %REGION% environment variable. .</summary>
        [EventField(EventConsts.runtimeREGION, OmitFromAudit = true)]
        public string RuntimeRegion => Environment.Region;

        /// <summary>The value of the %REGION% environment variable. .</summary>
        [EventField(EventConsts.runtimeZONE, OmitFromAudit = true)]
        public string RuntimeZone => Environment.Zone;

        /// <summary>The value of the %DC% environment variable. .</summary>
        [EventField(EventConsts.runtimeDC, OmitFromAudit = true)]
        [Obsolete("Deprecate after 2018; use region instead")]
        public string RuntimeDC => Environment.Zone;

        /// <summary>The value of the %ENV% environment variable. </summary>
        [EventField(EventConsts.runtimeENV, OmitFromAudit = true)]
        public string RuntimeENV => Environment.DeploymentEnvironment;


        //============ MESSAGE ===============
        public int? ErrCode { get; set; }

        /// <summary>Returns the explicitly-set <see cref="ErrCode"/>, or an error code deduced from the
        /// <see cref="Exception"/>, or null if neither was set.</summary>
        [EventField(EventConsts.errCode)]
        public int? ErrCode_ =>    ErrCode
                                ?? ((Exception as RequestException)?.ErrorCode
                                ?? (Exception != null ? 500001 //General Server Error
                                        : (int?)null));

        /// <summary>A short summary of the log event</summary>
        [EventField(EventConsts.message)]
        public string Message { get; set; }

        /// <summary>A detailed message, when relevant</summary>
        [EventField(EventConsts.details)]
        public string Details { get; set; }

        /// <summary>
        /// Exception to publish as individual fields
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>If an exception occured, the exception message.</summary>
        [EventField(EventConsts.exMessage)]
        internal string ExceptionMessage => Exception?.RawMessage();

        /// <summary> Used for grouping on the exception message in Kibana</summary>
        [EventField(EventConsts.exOneWordMessage)]
        public string ExceptionOneWordMessage => Exception?.RawMessage().Replace(' ', '_');

        /// <summary>If an exception occured and it contained inner exceptions, output a newline-separated list of messages,
        /// from outer-most to inner-most.</summary>
        [EventField(EventConsts.exInnerMessages)]
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

        [EventField(EventConsts.exOneWordInnerMessages)]
        public string OneWordInnerException => Exception?.InnerException?.RawMessage().Replace(' ', '_');

        [EventField(EventConsts.exInnerType)]
        public string InnerExceptionType => Exception?.InnerException?.GetType().FullName;

        private string _cleanStackTrace;

        /// <summary>If an exception occured, the exception stack trace.</summary>
        [EventField(EventConsts.exStackTrace)]
        public string ExceptionStackTrace
        {
            get
            {
                if (_cleanStackTrace != null)
                    return _cleanStackTrace;

                if (ShouldExcludeStackTrace || Exception == null)
                    return null;

                var ex = Exception;
                while (ex.StackTrace == null && ex.InnerException != null)
                    ex = ex.InnerException;

                _cleanStackTrace = StackTraceEnhancer?.Clean(ex.StackTrace);

                if (_cleanStackTrace?.Contains("__") == true)
                    ExceptionStackTraceIsUnclean = true;

                return _cleanStackTrace ?? ex.StackTrace;                
            }
        }

        [EventField(EventConsts.exStackTraceUnclean)]
        public bool? ExceptionStackTraceIsUnclean { get; private set; }

        /// <summary>The .Net type (full class name) of the exception.</summary>
        [EventField(EventConsts.exType)]
        public string ExceptionType => Exception?.GetType().FullName;

        /// <summary>Developer-provided details in key-value pairs form. Primitive types (int, float) will be indexed
        /// and be range-searchable.</summary>
        public Dictionary<string, object> UnencryptedTags { get; set; }

        /// <summary>Developer-provided details in key-value pairs form.</summary>
        public Dictionary<string, object> EncryptedTags { get; set; }

        [EventField(EventConsts.tags, AppendTypeSuffix = true)]
        public IEnumerable<KeyValuePair<string, object>> UnifiedUnencryptedTags =>
            Exception.GetUnencryptedTags().Concat(UnencryptedTags ?? Enumerable.Empty<KeyValuePair<string, object>>());

        [EventField(EventConsts.tags, Encrypt = true)]
        public IEnumerable<KeyValuePair<string, object>> UnifiedEncryptedTags =>
            Exception.GetEncryptedTagsAndExtendedProperties().Concat(EncryptedTags ?? Enumerable.Empty<KeyValuePair<string, object>>());

        /// <summary>The site ID, if applicable.</summary>
        [EventField(EventConsts.siteID)]
        public virtual ulong? SiteID { get; set; } = null;

        [EventField(EventConsts.apikey)]
        public virtual string ApiKey { get; set; } = null;

        [EventField(EventConsts.partnerID)]
        public virtual uint? PartnerID { get; set; } = null;

        /// <summary>Whether exception stack traces should be excluded. Note: can be overridden by derived classes.</summary>                
        public virtual bool ShouldExcludeStackTrace => Configuration.ExcludeStackTraceRule?.IsMatch(ErrCode_.ToString()) == true;

        [EventField(EventConsts.context, AppendTypeSuffix = true)]
        public IEnumerable<KeyValuePair<string, object>> ContextUnencryptedTags { get; set; } = TracingContext.TagsOrNull?.GetUnencryptedTags();

        [EventField(EventConsts.context, Encrypt = true)]
        public IEnumerable<KeyValuePair<string, object>> ContextTagsEncrypted { get; set; } = TracingContext.TagsOrNull?.GetEncryptedTags();
    }
}