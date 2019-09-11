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
                LoggerName = ReceivingType.Name,
                Namespace = ReceivingType.Namespace,
                ClassName = ReceivingType.Name,
                AssemblyName = reflectedAssembly.Name,
                AssemblyVersion = reflectedAssembly.Version.ToString(),
                BuildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
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
