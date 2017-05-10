using System;
using System.Net;

using Gigya.Microdot.Interfaces.Logging;

using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Hosting.Logging
{
	public sealed class OrleansLogConsumer : ILogConsumer
	{
		private ILog Log { get; }

		public OrleansLogConsumer(ILog log)
		{
			Log = log;
		}

		void ILogConsumer.Log(Severity severity, LoggerType loggerType, string caller, string message,
            IPEndPoint myIPEndPoint, Exception exception, int eventCode)
		{
			if (severity == Severity.Off)
				return;

            if (eventCode == 101705) // "Unable to find directory <bin folder>\Applications; skipping.."
                severity = Severity.Info;

            Action<LogDelegate> action = _ => _(message, exception: exception,
                unencryptedTags: new { loggerType, caller, EndPoint = myIPEndPoint?.Serialize(), eventCode });

            switch (severity)
            {
                // Do not convert the below calls to Log.Write(level, ...). They must be different method calls for our
                // logger to correctly cache call-site information. 

                case Severity.Error:
                    Log.Error(action);
                    break;
                case Severity.Warning:
                    Log.Warn(action);
                    break;
                case Severity.Info:
                    Log.Info(action);
                    break;
                case Severity.Verbose:
                case Severity.Verbose2:
                case Severity.Verbose3:
                    Log.Debug(action);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }
	}
}