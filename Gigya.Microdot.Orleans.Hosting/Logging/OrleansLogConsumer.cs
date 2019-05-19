//#region Copyright 
//// Copyright 2017 Gigya Inc.  All rights reserved.
//// 
//// Licensed under the Apache License, Version 2.0 (the "License"); 
//// you may not use this file except in compliance with the License.  
//// You may obtain a copy of the License at
//// 
////     http://www.apache.org/licenses/LICENSE-2.0
//// 
//// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
//// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
//// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
//// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
//// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
//// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
//// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
//// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
//// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
//// POSSIBILITY OF SUCH DAMAGE.
//#endregion

//using System;
//using System.Net;
//using Gigya.Microdot.Interfaces.Logging;
//using Orleans.Runtime;

//namespace Gigya.Microdot.Orleans.Hosting.Logging
//{
//	public sealed class OrleansLogConsumer : ILogConsumer
//	{
//		private ILog Log { get; }

//		public OrleansLogConsumer(ILog log)
//		{
//			Log = log;
//		}

//		void ILogConsumer.Log(Severity severity, LoggerType loggerType, string caller, string message,
//            IPEndPoint myIPEndPoint, Exception exception, int eventCode)
//		{
//			if (severity == Severity.Off)
//				return;

//            if (eventCode == 101705) // "Unable to find directory <bin folder>\Applications; skipping.."
//                severity = Severity.Info;

//            Action<LogDelegate> action = _ => _(message, exception: exception,
//                unencryptedTags: new { loggerType, caller, EndPoint = myIPEndPoint?.Serialize(), eventCode });

//            switch (severity)
//            {
//                // Do not convert the below calls to Log.Write(level, ...). They must be different method calls for our
//                // logger to correctly cache call-site information. 

//                case Severity.Error:
//                    Log.Error(action);
//                    break;
//                case Severity.Warning:
//                    Log.Warn(action);
//                    break;
//                case Severity.Info:
//                    Log.Info(action);
//                    break;
//                case Severity.Verbose:
//                case Severity.Verbose2:
//                case Severity.Verbose3:
//                    Log.Debug(action);
//                    break;
//                default:
//                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
//            }
//        }
//	}
//}