using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.SharedLogic.Logging
{
    public class LogCallSiteInfo
    {
        public Type ReflectedType;
        public string ClassName;
        public string AssemblyName;
        public string AssemblyVersion;
        public string MethodName;
        public string FileName;
        public int LineNumber;
        public string BuildTime;
    }

    public abstract class LogBase : ILog
    {

        protected LogCallSiteInfo CallSiteInfoTemplate { get; set; }


        public void Write(TraceEventType level, Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null)
        {
            if (MinimumTraceLevel < level)
                return;

            var logCallSiteInfo = new LogCallSiteInfo
            {
                ReflectedType = CallSiteInfoTemplate?.ReflectedType,
                ClassName = CallSiteInfoTemplate?.ClassName,
                AssemblyName = CallSiteInfoTemplate?.AssemblyName,
                AssemblyVersion = CallSiteInfoTemplate?.AssemblyVersion,
                MethodName = method,
                FileName = file,
                LineNumber = line,
                BuildTime = CallSiteInfoTemplate?.BuildTime,
            };

            try
            {
                log((message, encryptedTags, unencryptedTags, exception, includeStack) =>
                    {
                        var stackTrace = includeStack ? Environment.StackTrace : null;

                        //Some time people make mistake between encryptedTags and exception fields.
                        if (encryptedTags is Exception && exception == null)
                        {
                            exception = (Exception)encryptedTags;
                            encryptedTags = null;
                        }

                        var unencTags = TagsExtractor.GetTagsFromObject(unencryptedTags)
                                                     .Concat(exception.GetUnencryptedTags())
                                                     .Where(_ => _.Value != null)
                                                     .FormatTagsWithTypeSuffix()
                                                     .ToList();

                        var encTags = TagsExtractor.GetTagsFromObject(encryptedTags)
                                                   .Concat(exception.GetEncryptedTagsAndExtendedProperties())
                                                   .Where(_ => _.Value != null)
                                                   .FormatTagsWithoutTypeSuffix()
                                                   .ToList();

                        WriteLog(level, logCallSiteInfo, message, encTags, unencTags, exception, stackTrace);
                    });
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Programmatic error while logging: {ex}");
            }
        }


        protected abstract Task<bool> WriteLog(TraceEventType level, LogCallSiteInfo logCallSiteInfo, string message, List<KeyValuePair<string, string>> encTags, List<KeyValuePair<string, string>> unencTags, Exception exception= null, string stackTrace= null);

        public abstract TraceEventType? MinimumTraceLevel { get; set; }


        public void Debug(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null)
        {
            Write(TraceEventType.Verbose, log, file, line, method);
        }

        public void Info(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null)
        {
            Write(TraceEventType.Information, log, file, line, method);
        }

        public void Warn(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null)
        {
            Write(TraceEventType.Warning, log, file, line, method);
        }


        public void Warn(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null, bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            Write(TraceEventType.Warning, _ => _(message, encryptedTags, unencryptedTags, exception, includeStack), file, line, method);
        }


        public void Error(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null)
        {
            Write(TraceEventType.Error, log, file, line, method);
        }


        public void Error(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null, bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            Write(TraceEventType.Error, _ => _(message, encryptedTags, unencryptedTags, exception, includeStack), file, line, method);
        }


        public void Critical(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null)
        {
            Write(TraceEventType.Critical, log, file, line, method);
        }


        public void Critical(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null, bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            Write(TraceEventType.Critical, _ => _(message, encryptedTags, unencryptedTags, exception, includeStack), file, line, method);
        }

    }
}