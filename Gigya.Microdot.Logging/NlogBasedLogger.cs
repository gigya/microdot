using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

using Gigya.Microdot.SharedLogic.Logging;

using NLog;

namespace Gigya.Microdot.Logging
{

    public class NlogBasedLogger:LogBase
    {
        private readonly Logger logger;

        public NlogBasedLogger(Type receivingType)
        {
            
            logger = LogManager.GetCurrentClassLogger(receivingType);
            

            AssemblyName reflectedAssembly = receivingType.Assembly.GetName();
            CallSiteInfoTemplate = new LogCallSiteInfo
            {
                ReflectedType = receivingType,
                ClassName = receivingType.Name,
                AssemblyName = reflectedAssembly.Name,
                AssemblyVersion = reflectedAssembly.Version.ToString(),
                //BuildTime = Assembly.GetExecutingAssembly().TryGetBuildTime()?.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            
        }
        
        protected override Task<bool> WriteLog(TraceEventType level, 
            LogCallSiteInfo logCallSiteInfo, 
            string message, 
            List<KeyValuePair<string, string>> encTags, List<KeyValuePair<string, string>> unencTags, 
            Exception exception = null, 
            string stackTrace = null)
        {
            switch(level)
            {
                case TraceEventType.Critical:
                    if(logger.IsFatalEnabled)
                    {
                        logger.Log(LogLevel.Fatal, message);
                    }
                    break;
                case TraceEventType.Error:
                    if (logger.IsErrorEnabled)
                    {
                        logger.Log(LogLevel.Error, message);
                    }
                    break;
                case TraceEventType.Warning:
                    if (logger.IsWarnEnabled)
                    {
                        logger.Log(LogLevel.Warn, message);
                    }
                    break;
                case TraceEventType.Information:
                    if (logger.IsInfoEnabled)
                    {
                        logger.Log(LogLevel.Info, message);
                    }
                    break;
                case TraceEventType.Verbose:
                    if (logger.IsDebugEnabled)
                    {
                        logger.Log(LogLevel.Debug, message);
                    }
                    break;                    
            }

            return null;
        }

        public override TraceEventType? MinimumTraceLevel { get; set; }
    }
}
