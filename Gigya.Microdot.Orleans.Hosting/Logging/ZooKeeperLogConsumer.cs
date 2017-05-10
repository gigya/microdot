using System;
using System.Diagnostics;

using Gigya.Microdot.Interfaces.Logging;

using org.apache.utils;

namespace Gigya.Microdot.Orleans.Hosting.Logging
{
    public sealed class ZooKeeperLogConsumer : ILogConsumer
    {
        private ILog Log { get; }

        public ZooKeeperLogConsumer(ILog log)
        {
            Log = log;
        }

        void ILogConsumer.Log(TraceLevel traceLevel, string className, string message, Exception exception)
        {
            if (traceLevel == TraceLevel.Off)
                return;

            Action<LogDelegate> action = _ => _(message, exception: exception, unencryptedTags: new { className });

            switch (traceLevel)
            {
                // Do not convert the below calls to Log.Write(traceLevel, ...). They must be different method calls for
                // our logger to correctly cache call-site information. 

                case TraceLevel.Error:
                    Log.Error(action);
                    break;
                case TraceLevel.Warning:
                    Log.Warn(action);
                    break;
                case TraceLevel.Info:
                    Log.Info(action);
                    break;
                case TraceLevel.Verbose:
                    Log.Debug(action);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(traceLevel), traceLevel, null);
            }
        }
    }
}
