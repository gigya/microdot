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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic;


namespace Gigya.Microdot.Hosting.Service
{
    [ConfigurationRoot("Microdot.Hosting", RootStrategy.ReplaceClassNameWithPath)]
    public class MicrodotHostingConfig : IConfigObject
    {
        public bool FailServiceStartOnConfigError = true;
        public bool ExtendedDelaysTimeLogging = true;
        public List<string> StatusEndpoints = new List<string>();
        public bool ShouldLogStatusEndpoint = false;
        public bool GCEndpointEnabled = false;
        public TimeSpan? GCGetTokenCooldown = TimeSpan.FromHours(1);
    }

    [ConfigurationRoot("Microdot.Hosting.ThreadPool", RootStrategy.ReplaceClassNameWithPath)]
    public class MicrodotHostingThreadPoolConfig : IConfigObject
    {
        public bool MinThreadOverrideEnabled = true;
        public bool MaxThreadOverrideEnabled = false;
        public int MinWorkerThreads = 64;
        public int MinCompletionPortThreads = 64;
        public int MaxWorkerThreads = 32767;
        public int MaxCompletionPortThreads = 1000;
    }



    public abstract class ServiceHostBase : IDisposable
    {
        private bool _disposed;
        private readonly object _syncRoot = new object();

        public abstract string ServiceName { get; }

        public ServiceArguments Arguments { get; private set; }

        private ManualResetEvent StopEvent { get; }
        protected TaskCompletionSource<object> ServiceStartedEvent { get; set; }
        private TaskCompletionSource<StopResult> ServiceGracefullyStopped { get; set; }
        private Process MonitoredShutdownProcess { get; set; }
        protected ICrashHandler CrashHandler { get; set; }

        public virtual Version InfraVersion { get; }
        
        public bool? FailServiceStartOnConfigError { get; set; }

        public ServiceHostBase()
        {
            if (IntPtr.Size != 8)
                throw new Exception("You must run in 64-bit mode. Please make sure you unchecked the 'Prefer 32-bit' checkbox from the build section of the project properties.");


            StopEvent = new ManualResetEvent(true);
            ServiceStartedEvent = new TaskCompletionSource<object>();
            ServiceGracefullyStopped = new TaskCompletionSource<StopResult>();
            ServiceGracefullyStopped.SetResult(StopResult.None);
        }



        protected virtual void OnStop()
        {

        }

        /// <summary>
        /// Start the service, auto detecting between Windows service and command line. Always blocks until service is stopped.
        /// </summary>
        public void Run(ServiceArguments argumentsOverride = null)
        {
            ServiceGracefullyStopped = new TaskCompletionSource<StopResult>();

            try {
                Arguments = argumentsOverride ?? new ServiceArguments(System.Environment.GetCommandLineArgs().Skip(1).ToArray());
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ServiceArguments.GetHelpDocumentation());
                return;
            }

            if (argumentsOverride == null && ServiceArguments.IsHelpRequired(System.Environment.GetCommandLineArgs()))
            {
                Console.WriteLine(ServiceArguments.GetHelpDocumentation());
                return;
            }

            if (Arguments.ServiceStartupMode == ServiceStartupMode.VerifyConfigurations)
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
                        System.Environment.ExitCode = 1;
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
#if NET6_0_OR_GREATER
                if(Arguments.IsShutdownOnSignal)
                {
                    PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                    {
                        context.Cancel = true;
                        Console.WriteLine($"SIGTERM was recieved, shutting down...");
                        Stop();
                    });
                }
#endif
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
                bool isServiceGracefullyStopped = Task.Run(() => OnStop()).Wait(maxShutdownTime);

                if (isServiceGracefullyStopped == false)
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

        protected abstract void OnVerifyConfiguration();

        protected void VerifyConfiguration(ConfigurationVerificator ConfigurationVerificator)
        {
            if (ConfigurationVerificator == null)
            {
                System.Environment.ExitCode = 2;
                Console.Error.WriteLine("ERROR: The configuration verification is not properly implemented. " +
                                        "To implement you need to override OnVerifyConfiguration base method and call to base.");
            }
            else
            {
                try
                {
                    var results = ConfigurationVerificator.Verify();
                    System.Environment.ExitCode = results.All(r => r.Success) ? 0 : 1;

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
                    System.Environment.ExitCode = 3;
                    Console.Error.WriteLine(ex.Message);
                    Console.Error.WriteLine(ex.StackTrace);
                }
            }
        }

        protected void VerifyConfigurationsIfNeeded(
            MicrodotHostingConfig hostingConfig, ConfigurationVerificator configurationVerificator)
        {
            if (FailServiceStartOnConfigError??hostingConfig.FailServiceStartOnConfigError)
            {
                var badConfigs = configurationVerificator.Verify().Where(c => !c.Success).ToList();
                if (badConfigs.Any())
                    throw new EnvironmentException("Bad configuration(s) detected. Stopping service startup. You can disable this behavior through the Microdot.Hosting.FailServiceStartOnConfigError configuration. Errors:\n"
                        + badConfigs.Aggregate(new StringBuilder(), (sb, bc) => sb.Append(bc).Append('\n')));
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
        public virtual void Stop()
        {
            StopEvent.Set();
        }

        protected abstract void OnStart();

        protected void OnCrash()
        {
            Stop();
            WaitForServiceGracefullyStoppedAsync().Wait(5000);
            Dispose();
        }

        protected void SetThreadPoolConfigurations(MicrodotHostingThreadPoolConfig config)
        {
            if (config != null)
            {
                if (config.MinThreadOverrideEnabled == true)
                {
                    ThreadPool.SetMinThreads(config.MinWorkerThreads, config.MinCompletionPortThreads);
                }
                if (config.MaxThreadOverrideEnabled == true)
                {
                    ThreadPool.SetMaxThreads(config.MaxWorkerThreads, config.MaxCompletionPortThreads);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            SafeDispose(StopEvent);
            SafeDispose(MonitoredShutdownProcess);
        }


        public void Dispose()
        {
            lock (this._syncRoot)
            {
                try
                {
                    if (this._disposed)
                        return;

                    Dispose(false);
                }

                finally
                {
                    this._disposed = true;
                }
            }
        }

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

    }

    public enum StopResult { None, Graceful, Force}

}
