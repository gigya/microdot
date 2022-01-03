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
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Logging;

namespace Gigya.Microdot.Fakes
{
    public class LogSpy : FakeLog
    {
        public class LogEntry
        {
            public TraceEventType Severity { get; set; }
            public string Message { get; set; }
            public Dictionary<string, string> EncryptedTags { get; set; }
            public Dictionary<string, string> UnencryptedTags { get; set; }
            public Exception Exception { get; set; }
        }

        private List<LogEntry> LogEntriesList { get; } = new List<LogEntry>();

        public IEnumerable<LogEntry> LogEntries
        {
            get
            {
                lock (LogEntriesList)
                {
                    return LogEntriesList;
                }
            }
        }

        public void ClearLog()
        {
            lock (LogEntriesList)
            {
                LogEntriesList.Clear();
            }
        }


        protected override Task<bool> WriteLog(TraceEventType severity, LogCallSiteInfo logCallSiteInfo, string message, IDictionary<string, string> encryptedTags, IDictionary<string, string> unencryptedTags, Exception exception = null, string stackTrace = null)
        {            
            var entry = new LogEntry
            {
                Severity = severity,
                Message = message,
                EncryptedTags = encryptedTags.ToDictionary(_ => _.Key, _ => EventFieldFormatter.SerializeFieldValue(_.Value)),
                UnencryptedTags = unencryptedTags.ToDictionary(_ => _.Key, _ => EventFieldFormatter.SerializeFieldValue(_.Value)),
                Exception = exception
            };

            lock (LogEntriesList)
            {
                LogEntriesList.Add(entry);
            }

            return null;
        }

    }
}
