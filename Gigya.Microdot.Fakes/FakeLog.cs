using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Logging;

namespace Gigya.Microdot.Fakes
{

    public abstract class FakeLog : LogBase
    {
        protected FakeLog()
        {
            var ReceivingType = typeof(object);
            AssemblyName reflectedAssembly = ReceivingType.Assembly.GetName();
            CallSiteInfoTemplate = new LogCallSiteInfo
            {
                ReflectedType = ReceivingType,
                ClassName = ReceivingType.Name,
                AssemblyName = reflectedAssembly.Name,
                AssemblyVersion = reflectedAssembly.Version.ToString(),
                BuildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
        }
        public override TraceEventType? MinimumTraceLevel { get; set; } = TraceEventType.Information;
        public bool HideExceptionStackTrace { get; set; }
        public bool HideTags { get; set; }

        protected virtual string FormatLogEntry(TraceEventType severity, string message, List<KeyValuePair<string,string>> tags, Exception exception)
        {
            var sb = new StringBuilder(DateTime.Now.ToString("[HH:mm:ss.fff] "));

            switch (severity)
            {

                case TraceEventType.Critical:
                    sb.Append("CRITICAL ");
                    break;
                case TraceEventType.Error:
                    sb.Append("ERROR    ");
                    break;
                case TraceEventType.Warning:
                    sb.Append("WARNING  ");
                    break;
                case TraceEventType.Information:
                    sb.Append("INFO     ");
                    break;
                case TraceEventType.Verbose:
                    sb.Append("DEBUG    ");
                    break;
            }

            if (message != null)
                sb.Append(message);

            

            if (!HideTags && tags.Count > 0)
            {
                sb.Append(" { ");

                foreach (var tag in tags)
                    sb.Append($"{tag.Key}={EventFieldFormatter.SerializeFieldValue(tag.Value)}, ");

                sb.Remove(sb.Length - 2, 2);
                sb.Append(" }");
            }

            if (exception != null)
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                if(HideExceptionStackTrace)
                    sb.Append($"{exception.GetType().Namespace}: {exception.Message}");
                else
                    sb.Append(exception);
            }


            return sb.ToString();
        }
    }
}
