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
    public class OrleansLogAdapter : ILogger
    {
        private readonly OrleansLogEnrichment _logEnrichment;
        private readonly ILog _logImplementation;
        private readonly Func<OrleansConfig> _orleansConfigFunc;
        private readonly string _categoryUnderline;

        public OrleansLogAdapter(string category, Func<string, ILog> logImplementation, OrleansLogEnrichment logEnrichment, Func<OrleansConfig> orleansConfigFunc)
        {
            _categoryUnderline = category?.Replace(".", "_"); // The element in config, contains '_' for every '.' in class name
            _logEnrichment = logEnrichment;
            _orleansConfigFunc = orleansConfigFunc;
            _logImplementation = logImplementation(category);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            string eventHeuristicName = null;
            if (eventId.Name == null)
            {
                _logEnrichment.HeuristicEventIdToName.TryGetValue(eventId.Id, out eventHeuristicName);
            }

            var logMessage = formatter(state, exception);

            Action<LogDelegate> action = _ => _(logMessage, exception: exception, unencryptedTags: new { eventId.Id, eventId.Name, IsOrleansLog = true, eventHeuristicName });
            var level = TraceEventType.Critical;
            switch (logLevel)
            {
                case LogLevel.Trace:
                    level = TraceEventType.Verbose;
                    break;
                case LogLevel.Debug:
                    level = TraceEventType.Verbose;
                    break;
                case LogLevel.Information:
                    level = TraceEventType.Information;
                    break;
                case LogLevel.Warning:
                    level = TraceEventType.Warning;
                    break;
                case LogLevel.Error:
                    level = TraceEventType.Error;
                    break;
                case LogLevel.Critical:
                    level = TraceEventType.Critical;
                    break;
                case LogLevel.None:
                    return;
            }
            _logImplementation.Write(level, action);

        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // #ORLEANS2 [Done] We paid attention to massive GC when deactivation of huge amount of grains
            //           as orleans code concatenates grain ids for the log entry
            //           see more details in https://github.com/dotnet/orleans/issues/5851
            
            var config = _orleansConfigFunc();

            // Configure the log level according to the category.
            if(_categoryUnderline != null && config.CategoryLogLevels.Count >0)
                if (config.CategoryLogLevels.TryGetValue(_categoryUnderline, out var configLogLevel))
                {
                    return logLevel >= configLogLevel.LogLevel;
                }

            return logLevel >= config.DefaultCategoryLogLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public void Write(TraceEventType level, Action<LogDelegate> log, string file = "", int line = 0, string method = null)
        {
            _logImplementation.Write(level, log, file, line, method);
        }
    }
}