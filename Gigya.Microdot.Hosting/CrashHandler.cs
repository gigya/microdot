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
using System.Threading.Tasks;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.SharedLogic.Events;

namespace Gigya.Microdot.Hosting
{
    public class CrashHandler
    {
        /// <summary>
        /// Only in Orleans host we need to signal to the other cluster that this silo is down,
        /// so it will no longer receive messages and can be close with no traffic lost
        /// </summary>
        public Action SignalClusterThatThisNodeIsGoingDown { get; set; }
        private IEventPublisher<CrashEvent> Publisher { get; }
        private bool WasTriggered { get; set; }
        private object SyncLock { get; }

        public CrashHandler(Action signalClusterThatThisNodeIsGoingDown, IEventPublisher<CrashEvent> publisher)
        {
            SignalClusterThatThisNodeIsGoingDown = signalClusterThatThisNodeIsGoingDown;
            Publisher = publisher;
            SyncLock = new object();
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            lock (SyncLock)
            {
                if (WasTriggered)
                    return;

                WasTriggered = true;

                Console.WriteLine("***  CrashHandler: CRASH DETECTED!");

                try
                {
                    var exception = args.ExceptionObject as Exception;
                    var requestId = Guid.NewGuid().ToString("N");

                    Console.WriteLine($"***  CrashHandler: Publishing crash event with callID: {requestId} [{exception?.GetType().Name}] {exception?.Message}");
                    
                    var evt = Publisher.CreateEvent();
                    evt.Exception = exception;
                    evt.RequestId = requestId;
                    
                    if (Publisher.TryPublish(evt).PublishEvent.Wait(10000) == false)
                        throw new TimeoutException("Event failed to publish within 10 second timeout.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"***  CrashHandler: Failed to publish event. Exception thrown while attempting to write the event: [{ex.GetType().Name}] {ex.Message}");
                }

                try
                {
                    Console.WriteLine("***  CrashHandler: Attempting to gracefully shut down service...");
                    
                    var sw = Stopwatch.StartNew();
                    Task.Run(SignalClusterThatThisNodeIsGoingDown).Wait(TimeSpan.FromSeconds(10));
                    
                    Console.WriteLine($"***  CrashHandler: Service successfully shut down after {sw.Elapsed}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"***  CrashHandler: Failed to shut down service - [{ex.GetType().Name}] {ex.Message}");
                }
            }
        }
    }

    public class CrashEvent : Event
    {
        [EventField("event")]
        public string Event { get; } = "crash";

        public override bool ShouldExcludeStackTrace => false;

        private readonly Process _currentProcess = Process.GetCurrentProcess();

        [EventField("process.pid")]
        public int ProcessId => _currentProcess.Id;

        [EventField("process.path")]
        public string ProcessPath => _currentProcess.MainModule.FileName;

        [EventField("process.memoryMB")]
        public double ProcessMemoryMb => _currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0;

        [EventField("process.totalCpuTime")]
        public string ProcessTotalCpuTime => _currentProcess.TotalProcessorTime.ToString();

        [EventField("process.uptime")]
        public string ProcessUptime => (DateTime.Now - _currentProcess.StartTime).ToString(); // Must compare to DateTime.Now and not UtcNow.

        [EventField("ex.toString")]
        public string ExceptionToString => Exception?.ToString();
    }
}
