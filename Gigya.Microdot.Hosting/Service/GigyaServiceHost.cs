using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

using Gigya.Microdot.SharedLogic;

[assembly: InternalsVisibleTo("LINQPadQuery")]

namespace Gigya.Microdot.Hosting.Service
{
    public abstract class GigyaServiceHost:IDisposable
    {

        const int WANR_IF_SHUTDOWN_LONGER_THAN_SECS = 10;

        private bool disposed = false;

        public ServiceArguments Arguments { get; private set; }

        private DelegatingServiceBase WindowsService { get; set; }
        private ManualResetEvent StopEvent { get; }
        private TaskCompletionSource<object> ServiceStartedEvent { get; set; }
        private Process MonitoredShutdownProcess { get; set; }

        /// <summary>
        /// The name of the service. This will be globally accessible from <see cref="CurrentApplicationInfo.Name"/>.
        /// </summary>
        protected abstract string ServiceName { get; }

        protected GigyaServiceHost()
        {
            if (IntPtr.Size != 8)
                throw new Exception("You must run in 64-bit mode. Please make sure you unchecked the 'Prefer 32-bit' checkbox from the build section of the project properties.");

            StopEvent = new ManualResetEvent(true);
            ServiceStartedEvent = new TaskCompletionSource<object>();
        }

        /// <summary>
        /// Start the service, autodetecting between Windows service and command line. Always blocks until service is stopped.
        /// </summary>
        public void Run(ServiceArguments argumentsOverride = null)
        {
            Arguments = argumentsOverride ?? new ServiceArguments(Environment.GetCommandLineArgs().Skip(1).ToArray());
            CurrentApplicationInfo.Init(ServiceName, Arguments.InstanceName);

            if (Arguments.ServiceStartupMode == ServiceStartupMode.WindowsService)
            {
                Trace.WriteLine("Service starting as a Windows service...");
                WindowsService = new DelegatingServiceBase(ServiceName, OnWindowsServiceStart, OnWindowsServiceStop);

                if (argumentsOverride == null)
                    Arguments = null; // Ensures OnWindowsServiceStart reloads parameters passed from Windows Service Manager.

                ServiceBase.Run(WindowsService); // This calls OnWindowsServiceStart() on a different thread and blocks until the service stops.
            }
            else
            {
                OnStart();

                if (Arguments.ShutdownWhenPidExits != null)
                {
                    Console.WriteLine($"Will perform graceful shutdown when PID {Arguments.ShutdownWhenPidExits} exits.");
                    MonitoredShutdownProcess = Process.GetProcessById(Arguments.ShutdownWhenPidExits.Value);
                    MonitoredShutdownProcess.Exited += (s, a) =>
                    {
                        Console.WriteLine($"PID {Arguments.ShutdownWhenPidExits} has exited, shutting down...");
                        Stop();
                    };
                    MonitoredShutdownProcess.EnableRaisingEvents = true;
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
                var cancelShutdownMonitoring = new CancellationTokenSource();
                VerifyStuckedShutDown(cancelShutdownMonitoring.Token);
                OnStop();
                cancelShutdownMonitoring.Cancel();
                ServiceStartedEvent = new TaskCompletionSource<object>();
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
        /// Waits for the service to finish starting. Mainly used from tests.
        /// </summary>
        public Task WaitForServiceStartedAsync()
        {
            return ServiceStartedEvent.Task;
        }


        /// <summary>
        /// Signals the service to stop.
        /// </summary>
        public void Stop()
        {
            if (StopEvent.WaitOne(0))
                throw new InvalidOperationException("Service is already stopped, or is running in an unsupported mode.");

            StopEvent.Set();
        }


        private void OnWindowsServiceStart(string[] args)
        {
            if (Arguments == null)
            {
                Arguments = new ServiceArguments(args);
                CurrentApplicationInfo.Init(ServiceName, Arguments.InstanceName);
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

            if(disposing)
            {                
                StopEvent.Dispose();
                WindowsService?.Dispose();
                MonitoredShutdownProcess?.Dispose();
            }

            disposed = true;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        // temporary method to detect if we lock up during shutdown (some services were observed to not shut down properly).
        // since we don't have a logger at that time, we write to a file (nomad does the same when it needs to hard-kill a service).
        // remove this after Q1 2017 if it never triggers
        void VerifyStuckedShutDown(CancellationToken cancel)
        {
            var shutdownThread = Thread.CurrentThread;
            Task.Run(async () =>
            {
                await Task.Delay(WANR_IF_SHUTDOWN_LONGER_THAN_SECS * 1000);
                if (!cancel.IsCancellationRequested)
                    try {
                        shutdownThread.Suspend();
                        var message = $"Application stuck during shut down for over {WANR_IF_SHUTDOWN_LONGER_THAN_SECS} seconds! Stack trace:\n";
                        var stackTrace = new StackTrace(shutdownThread, needFileInfo: true);
                        File.WriteAllText($"_{CurrentApplicationInfo.Name}_slow_shutdown_{DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss")}.log", message + stackTrace.ToString());
                    }
                    catch (Exception ex) { }
                    finally
                    {
                        try { shutdownThread.Resume(); }
                        catch { }
                    }
            });
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
}
