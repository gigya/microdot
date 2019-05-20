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
using System.Diagnostics;
using Gigya.Microdot.Interfaces.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;

namespace Gigya.Microdot.Orleans.Hosting.Logging
{
    public class OrleansLogAdapter : ILogger, ILog
    {
        private readonly ILog _logImplementation;

        public OrleansLogAdapter(string category, Func<string, ILog> logImplementation)
        {
            _logImplementation = logImplementation(category);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {

            var logMessage = formatter(state, exception);

            Action<LogDelegate> action = _ => _(logMessage, exception: exception, unencryptedTags: new { eventId.Id, eventId.Name, IsOrleansLog = true });
            var level = TraceEventType.Critical;
            switch (logLevel)
            {
                case LogLevel.Critical:
                    level = TraceEventType.Critical;
                    break;
                case LogLevel.Debug:
                    level = TraceEventType.Verbose;
                    break;
                case LogLevel.Trace:
                    level = TraceEventType.Verbose;
                    break;
                case LogLevel.Information:
                    level = TraceEventType.Information;
                    break;
                case LogLevel.Error:
                    level = TraceEventType.Error;
                    break;
                case LogLevel.Warning:
                    level = TraceEventType.Warning;
                    break;
                case LogLevel.None:
                    return;
            }
            _logImplementation.Write(level, action);

        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public void Debug(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Debug(log, file, line, method);
        }

        public void Info(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Info(log, file, line, method);
        }

        public void Warn(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Warn(log, file, line, method);
        }

        public void Warn(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null,
            bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Warn(message, encryptedTags, unencryptedTags, exception, includeStack, file, line, method);
        }

        public void Error(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Error(log, file, line, method);
        }

        public void Error(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null,
            bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Error(message, encryptedTags, unencryptedTags, exception, includeStack, file, line, method);
        }

        public void Critical(Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Critical(log, file, line, method);
        }

        public void Critical(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null,
            bool includeStack = false, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Critical(message, encryptedTags, unencryptedTags, exception, includeStack, file, line, method);
        }

        public void Write(TraceEventType level, Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Write(level, log, file, line, method);
        }
    }
}