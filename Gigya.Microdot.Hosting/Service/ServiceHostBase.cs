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
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Hosting.Service
{
    public abstract class ServiceHostBase : IDisposable
    {
        private bool disposed;

        public ServiceArguments Arguments { get; private set; }

        private DelegatingServiceBase WindowsService { get; set; }
        private ManualResetEvent StopEvent { get; }
        protected TaskCompletionSource<object> ServiceStartedEvent { get; set; }
        private TaskCompletionSource<StopResult> ServiceGracefullyStopped { get; set; }
        private Process MonitoredShutdownProcess { get; set; }
        protected ICrashHandler CrashHandler { get; set; }

        /// <summary>
        /// The name of the service. This will be globally accessible from <see cref="CurrentApplicationInfo.Name"/>.
        /// </summary>
        public abstract string ServiceName { get; }

         
        protected virtual ConfigurationVerificator ConfigurationVerificator { get; set; }

        /// <summary>
        /// Version of wrapping infrastructure framework. This will be globally accessible from <see cref="CurrentApplicationInfo.InfraVersion"/>.
        /// </summary>
        protected virtual Version InfraVersion => null;

        protected ServiceHostBase()
        {
            if (IntPtr.Size != 8)
                throw new Exception("You must run in 64-bit mode. Please make sure you unchecked the 'Prefer 32-bit' checkbox from the build section of the project properties.");


            StopEvent = new ManualResetEvent(true);
            ServiceStartedEvent = new TaskCompletionSource<object>();
            ServiceGracefullyStopped = new TaskCompletionSource<StopResult>();
            ServiceGracefullyStopped.SetResult(StopResult.None);

         
        }


        /// <summary>
        /// Start the service, auto detecting between Windows service and command line. Always blocks until service is stopped.
        /// </summary>
        public void Run(ServiceArguments argumentsOverride = null)
        {
            ServiceGracefullyStopped = new TaskCompletionSource<StopResult>();
            Arguments = argumentsOverride ?? new ServiceArguments(Environment.GetCommandLineArgs().Skip(1).ToArray());
            
            

            if (Arguments.ServiceStartupMode == ServiceStartupMode.WindowsService)
            {
                Trace.WriteLine("Service starting as a Windows service...");
                WindowsService = new DelegatingServiceBase(ServiceName, OnWindowsServiceStart, OnWindowsServiceStop);

                if (argumentsOverride == null)
                    Arguments = null; // Ensures OnWindowsServiceStart reloads parameters passed from Windows Service Manager.

                ServiceBase.Run(WindowsService); // This calls OnWindowsServiceStart() on a different thread and blocks until the service stops.
            }
            else if (Arguments.ServiceStartupMode == ServiceStartupMode.VerifyConfigurations)
            {
                OnVerifyConfiguration();
            }
            else
            {
                if (Arguments.ShutdownWhenPidExits != null)
                {
                    try
                    {
                        MonitoredShutdownProcess = Process.GetProcessById(Arguments.ShutdownWhenPidExits.Value);
                    }
                    catch (ArgumentException e)
                    {
                        Console.WriteLine($"Service cannot start because monitored PID {Arguments.ShutdownWhenPidExits} is not running. Exception: {e}");
                        Environment.ExitCode = 1;
                        ServiceGracefullyStopped.SetResult(StopResult.None);
                        return;
                    }

                    Console.WriteLine($"Will perform graceful shutdown when PID {Arguments.ShutdownWhenPidExits} exits.");
                    MonitoredShutdownProcess.Exited += (s, a) =>
                    {
                        Console.WriteLine($"PID {Arguments.ShutdownWhenPidExits} has exited, shutting down...");
                        Stop();
                    };

                    MonitoredShutdownProcess.EnableRaisingEvents = true;
                }

                try
                {
                    OnStart();
                }
                catch (Exception e)
                {
                    ServiceStartedEvent.TrySetException(e);

                    throw;
                }

                if (Arguments.ServiceStartupMode == ServiceStartupMode.CommandLineInteractive)
                {
                    Thread.Sleep(10); // Allow any startup log messages to flush to Console.

                    Console.Title = ServiceName;

                    if (Arguments.ConsoleOutputMode == ConsoleOutputMode.Color)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Service initialized in interactive mode (command line). Press ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.BackgroundColor = ConsoleColor.DarkCyan;
                        Console.Write("[Alt+S]");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.WriteLine(" to stop the service gracefully.");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.WriteLine("Service initialized in interactive mode (command line). Press [Alt+S] to stop the service gracefully.");
                    }

                    Task.Factory.StartNew(() =>
                             {
                                 while (true)
                                 {
                                     var key = Console.ReadKey(true);

                                     if (key.Key == ConsoleKey.S && key.Modifiers == ConsoleModifiers.Alt)
                                     {
                                         Stop();
                                         break;
                                     }
                                 }
                             }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }
                else
                {
                    Console.WriteLine("Service initialized in non-interactive mode (command line). Waiting for stop request...");
                }

                StopEvent.Reset();
                ServiceStartedEvent.SetResult(null);
                StopEvent.WaitOne();

                Console.WriteLine("   ***   Shutting down...   ***   ");

                var maxShutdownTime = TimeSpan.FromSeconds((Arguments.OnStopWaitTimeSec ?? 0) + (Arguments.ServiceDrainTimeSec ?? 0));
                bool isServiceGracefullyStopped =  Task.Run(() => OnStop()).Wait(maxShutdownTime);

                if( isServiceGracefullyStopped ==false )
                    Console.WriteLine($"   ***  Service failed to stop gracefully in the allotted time ({maxShutdownTime}), continuing with forced shutdown.   ***   ");

                ServiceStartedEvent = new TaskCompletionSource<object>();

                ServiceGracefullyStopped.SetResult(isServiceGracefullyStopped ? StopResult.Graceful : StopResult.Force);
                MonitoredShutdownProcess?.Dispose();

                if (Arguments.ServiceStartupMode == ServiceStartupMode.CommandLineInteractive)
                {
                    if (Arguments.ConsoleOutputMode == ConsoleOutputMode.Color)
                    {
                        Console.BackgroundColor = ConsoleColor.White;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.WriteLine("   ***   Shutdown complete. Press any key to exit.   ***   ");
                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.WriteLine("   ***   Shutdown complete. Press any key to exit.   ***   ");
                    }

                    Console.ReadKey(true);
                }
            }
        }

        /// <summary>
        /// An extensibility point - this method is called in process of configuration objects verification.
        /// </summary>
        protected virtual void OnVerifyConfiguration()
        {
            if (ConfigurationVerificator == null)
            {
                Environment.ExitCode = 2;
                Console.Error.WriteLine("ERROR: The configuration verification is not properly implemented. " +
                                        "To implement you need to override OnVerifyConfiguration base method and call to base.");
            }
            else
            {
                try
                {
                    var results = ConfigurationVerificator.Verify();
                    Environment.ExitCode = results.All(r => r.Success) ? 0 : 1;

                    if (Arguments.ConsoleOutputMode == ConsoleOutputMode.Color)
                    {
                        var (restoreFore, restoreBack) = (Console.ForegroundColor, Console.BackgroundColor);
                        foreach (var result in results)
                        {
                            Console.BackgroundColor = result.Success ? ConsoleColor.Black : ConsoleColor.White;
                            Console.ForegroundColor = result.Success ? ConsoleColor.White : ConsoleColor.Red;
                            Console.WriteLine(result);
                        }
                        Console.BackgroundColor = restoreBack;
                        Console.ForegroundColor = restoreFore;
                    }
                    else  
                    {
                        foreach (var result in results)
                            Console.WriteLine(result);
                    }
                    // Avoid extra messages in machine to machine mode or when disabled
                    if (!(Console.IsOutputRedirected || Arguments.ConsoleOutputMode == ConsoleOutputMode.Disabled))
                        Console.WriteLine("   ***   Shutting down [configuration verification mode].   ***   ");
                }
                catch (Exception ex)
                {
                    Environment.ExitCode = 3;
                    Console.Error.WriteLine(ex.Message);
                    Console.Error.WriteLine(ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Waits for the service to finish starting. Mainly used from tests.
        /// </summary>
        public Task WaitForServiceStartedAsync()
        {
            return ServiceStartedEvent.Task;
        }

        public Task<StopResult> WaitForServiceGracefullyStoppedAsync()
        {
            return ServiceGracefullyStopped.Task;
        }


        /// <summary>
        /// Signals the service to stop.
        /// </summary>
        public void Stop()
        {
                StopEvent.Set();
        }

        protected virtual void OnCrash()
        {
            Stop();
            WaitForServiceGracefullyStoppedAsync().Wait(5000);
            Dispose();
        }


        private void OnWindowsServiceStart(string[] args)
        {
            if (Arguments == null)
            {
                Arguments = new ServiceArguments(args);
            }

            try
            {
                if (Arguments.ServiceStartupMode != ServiceStartupMode.WindowsService)
                    throw new InvalidOperationException($"Cannot start in {Arguments.ServiceStartupMode} mode when starting as a Windows service.");

                if (Environment.UserInteractive == false)
                {
                    throw new InvalidOperationException(
                        "This Windows service requires to be run with 'user interactive' enabled to correctly read certificates. " +
                        "Either the service wasn't configure with the 'Allow service to interact with desktop' option enabled " +
                        "or the OS is ignoring the checkbox due to a registry settings. " +
                        "Make sure both the checkbox is checked and following registry key is set to DWORD '0':\n" +
                        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Windows\NoInteractiveServices");
                }

                WindowsService.RequestAdditionalTime(60000);

                OnStart();
            }
            catch
            {
                WindowsService.ExitCode = 1064; // "An exception occurred in the service when handling the control request." (net helpmsg 1064)
                throw;
            }
        }


        private void OnWindowsServiceStop()
        {
            WindowsService.RequestAdditionalTime(60000);

            try
            {                
                OnStop();
            }
            catch
            {
                WindowsService.ExitCode = 1064; // "An exception occurred in the service when handling the control request." (net helpmsg 1064)
                throw;
            }

        }
        

        protected abstract void OnStart();
        protected abstract void OnStop();


        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            disposed = true;

            SafeDispose(StopEvent);
            SafeDispose(WindowsService);
            SafeDispose(MonitoredShutdownProcess);
        }


        public void Dispose() => Dispose(false);

        protected void SafeDispose(IDisposable disposable)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());
            }
        }


        private class DelegatingServiceBase : ServiceBase
        {
            private readonly Action<string[]> _onStart;
            private readonly Action _onStop;


            public DelegatingServiceBase(string serviceName, Action<string[]> onStart, Action onStop)
            {
                ServiceName = serviceName; // Required for auto-logging to event viewer of start/stop event and exceptions.
                _onStart = onStart;
                _onStop = onStop;
            }


            protected override void OnStart(string[] args)
            {
                _onStart(args);
            }


            protected override void OnStop()
            {
                _onStop();
            }
        }
    }
    public enum StopResult { None, Graceful, Force}

}
