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
using System.Linq;

namespace Gigya.Microdot.SharedLogic
{
    /// <summary>
    /// Specifies arguments for starting a service, typically supplied via command-line arguments.
    /// </summary>
    [Serializable]
    public class ServiceArguments
    {
        /// <summary>
        /// Specifies under which mode to start the service (command line, Windows service, etc).
        /// </summary>
        public ServiceStartupMode ServiceStartupMode { get; private set; }

        /// <summary>
        /// Specifies how to output log messages to the console, if at all.
        /// </summary>
        public ConsoleOutputMode ConsoleOutputMode { get; private set; }

        /// <summary>
        /// Specifies how a silo started in this service should behave in a cluster. Not relevant for non-Orleans services.
        /// </summary>
        public SiloClusterMode SiloClusterMode { get; private set; }

        /// <summary>
        /// Specifies what base port should be used for the silo. Not relevant for non-Orleans services. This value
        /// takes precedence over any base port overrides in configuration.
        /// </summary>
        public int? BasePortOverride { get; }

        /// <summary>
        /// Slot Number value in the range (1..[Range Size - 1]). By default the valid offsets slot numbers will be in the range 1-999
        /// It used in calculating service ports like this [BasePort]+[RangeSize]*[Protocol]+[SlotNumber].
        /// </summary>
        public int? SlotNumber { get; }

        /// <summary>
        /// Logical instance name for the current application, which can be used to differentiate between
        /// multiple identical applications running on the same host.
        /// </summary>
        public string InstanceName { get; }

        /// <summary>
        /// Specifies the process ID of a process that should be monitored. When the process exists, a graceful shutdown
        /// is performed.
        /// </summary>
        public int? ShutdownWhenPidExits { get; }

        /// <summary>
        /// Specifies drain time in this time the service status will be 521.
        /// </summary>
        public int? ServiceDrainTimeSec { get;  }

        /// <summary>
        /// Defines the time to wait for the service to stop, default is 10 seconds. Only after OnStopWaitTimeSec+ServiceDrainTimeSec the service will be forcibly closed.
        /// </summary>
        public int? OnStopWaitTimeSec { get; private set; }

        /// <summary>
        /// Defines the time to wait for the service to start, if elapsed, the service start aborted. Default is 180 seconds.
        /// </summary>
        public int? InitTimeOutSec { get; private set; }

        /// <summary>
        /// Secondary nodes without ZooKeeper are only supported on a developer's machine (or unit tests), so
        /// localhost and the original base port are always assumed (since the secondary nodes must use a
        /// base port override to avoid port conflicts).
        ///</summary> 
        public int? SiloNetworkingPortOfPrimaryNode{ get ; set ; }


        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceArguments"/> class, explicitly specifying the arguments.
        /// Typically used in tests.
        /// </summary>
        /// <param name="serviceStartupMode">Optional. The service startup mode to use.</param>
        /// <param name="consoleOutputMode">Optional. The console output mode to use.</param>
        /// <param name="siloClusterMode">Optional. The silo cluster mode to use.</param>
        /// <param name="basePortOverride">Optional. The base port override to use, or null to not override the base port.</param>
        public ServiceArguments(ServiceStartupMode serviceStartupMode = ServiceStartupMode.Unspecified,
                                ConsoleOutputMode consoleOutputMode = ConsoleOutputMode.Unspecified,
                                SiloClusterMode siloClusterMode = SiloClusterMode.Unspecified,
                                int? basePortOverride = null, string instanceName = null,
                                int? shutdownWhenPidExits = null, int? slotNumber = null, int? onStopWaitTimeSec=null,int? serviceDrainTimeSec=null, int?  initTimeOutSec=null)
        {
            ServiceStartupMode = serviceStartupMode;
            ConsoleOutputMode = consoleOutputMode;
            SiloClusterMode = siloClusterMode;
            BasePortOverride = basePortOverride;
            InstanceName = instanceName;
            ShutdownWhenPidExits = shutdownWhenPidExits;
            SlotNumber = slotNumber;
            OnStopWaitTimeSec = onStopWaitTimeSec;
            ServiceDrainTimeSec = serviceDrainTimeSec;
            InitTimeOutSec = initTimeOutSec;
            ApplyDefaults();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceArguments"/> class, parsing the values from the supplied
        /// string arguments. Typically used to parse command-line arguments.
        /// </summary>
        /// <param name="args">An array of strings, each containing a single argument.</param>
        public ServiceArguments(string[] args)
        {
            ServiceStartupMode = ParseEnumArg<ServiceStartupMode>(args);
            ConsoleOutputMode = ParseEnumArg<ConsoleOutputMode>(args);
            SiloClusterMode = ParseEnumArg<SiloClusterMode>(args);
            BasePortOverride = TryParseInt(ParseStringArg(nameof(BasePortOverride), args));
            InstanceName = ParseStringArg(nameof(InstanceName), args);
            ShutdownWhenPidExits = TryParseInt(ParseStringArg(nameof(ShutdownWhenPidExits), args));
            SlotNumber = TryParseInt(ParseStringArg(nameof(SlotNumber), args));
            OnStopWaitTimeSec = TryParseInt(ParseStringArg(nameof(OnStopWaitTimeSec), args));
            ServiceDrainTimeSec = TryParseInt(ParseStringArg(nameof(ServiceDrainTimeSec), args));
            InitTimeOutSec = TryParseInt(ParseStringArg(nameof(InitTimeOutSec), args));

            ApplyDefaults();
        }


        private static int? TryParseInt(string str) { return int.TryParse(str, out int val) ? (int?)val : null; }


        private void ApplyDefaults()
        {
            if(ServiceStartupMode == ServiceStartupMode.Unspecified)
            {
                if(Console.IsInputRedirected)
                    ServiceStartupMode = ServiceStartupMode.CommandLineNonInteractive;
                else
                    ServiceStartupMode = ServiceStartupMode.CommandLineInteractive;
            }

            // ReSharper disable SwitchStatementMissingSomeCases
            if (ConsoleOutputMode == ConsoleOutputMode.Unspecified)
            {
                switch (ServiceStartupMode)
                {
                    case ServiceStartupMode.CommandLineInteractive:
                        ConsoleOutputMode = ConsoleOutputMode.Color;
                        break;
                    case ServiceStartupMode.VerifyConfigurations:
                        ConsoleOutputMode = Console.IsInputRedirected ? ConsoleOutputMode.Standard : ConsoleOutputMode.Color;
                        break;
                    case ServiceStartupMode.CommandLineNonInteractive:
                        ConsoleOutputMode = ConsoleOutputMode.Standard;
                        break;
                    case ServiceStartupMode.WindowsService:
                        ConsoleOutputMode = ConsoleOutputMode.Disabled;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (SiloClusterMode == SiloClusterMode.Unspecified)
            {
                switch (ServiceStartupMode)
                {
                    case ServiceStartupMode.CommandLineInteractive:
                    case ServiceStartupMode.CommandLineNonInteractive:
                    case ServiceStartupMode.VerifyConfigurations:
                        SiloClusterMode = SiloClusterMode.PrimaryNode;
                        break;
                    case ServiceStartupMode.WindowsService:
                        SiloClusterMode = SiloClusterMode.ZooKeeper;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (OnStopWaitTimeSec == null)
                OnStopWaitTimeSec = 10;
            if (InitTimeOutSec == null)
                InitTimeOutSec = 60*3;
            // ReSharper restore SwitchStatementMissingSomeCases
        }


        private static T ParseEnumArg<T>(string[] args)
        {
            var arg = args.FirstOrDefault(a => a.StartsWith($"--{typeof(T).Name}:", StringComparison.InvariantCultureIgnoreCase));

            if (arg == null)
                return default(T);

            return (T)Enum.Parse(typeof(T), arg.Split(':').Last(), true);
        }


        private static string ParseStringArg(string name, string[] args)
        {
            return args.FirstOrDefault(a => a.StartsWith($"--{name}:", StringComparison.InvariantCultureIgnoreCase))
                ?.Split(':').Last();
        }
    }


    /// <summary>
    /// Specifies how to start a host
    /// </summary>
    public enum ServiceStartupMode
    {
        /// <summary>
        /// Default. This value will be overwritten by a smart default as described on the other enum values.
        /// </summary>
        Unspecified,
        
        /// <summary>
        /// Specifies that the service will run in command-line mode, expecting input from the user. This is the default
        /// value when compiled with 'Debug' when Console.IsInputRedirected == false.
        /// </summary>
        CommandLineInteractive,

        /// <summary>
        /// Specifies that the service will run in command-line mode, not requiring any input from the user. This is the
        /// default value when compiled with 'Debug' when Console.IsInputRedirected == true.
        /// </summary>
        CommandLineNonInteractive,

        /// <summary>
        /// Specifies that the service will run as a Windows service. This is the default value when compiled with
        /// 'Release'.
        /// </summary>
        WindowsService,

		/// <summary>
		/// Specifies that the service will run to verify configuration objects only (while performing only minimal required initialization).
		/// Available IConfigObject implementations (config objects) in service assemblies will be instantiated to pass validations.
		/// </summary>
		/// <remarks>
		/// The validation errors will be reported in console. Config objects will be discovered with <see cref="AssemblyProvider"/>
		/// considering assemblies black list.
		/// </remarks>
	    VerifyConfigurations
    }


    /// <summary>
    /// Specifies how log messages should be written to the console.
    /// </summary>
    public enum ConsoleOutputMode
    {
        /// <summary>
        /// Default. This value will be overwritten by a smart default as described on the other enum values.
        /// </summary>
        Unspecified,

        /// <summary>
        /// Specifies that log messages should be written to the console with coloring. This is the default when running
        /// as <see cref="ServiceStartupMode.CommandLineInteractive" />.
        /// </summary>
        Color,

        /// <summary>
        /// Specifies that log messages should be written to the console without coloring. This is the default when
        /// running as <see cref="ServiceStartupMode.CommandLineNonInteractive" />.
        /// </summary>
        Standard,

        /// <summary>
        /// Specifies that log messages should not be written to the console at all. This is the default when running
        /// as <see cref="ServiceStartupMode.WindowsService" />.
        /// </summary>
        Disabled
    }


    /// <summary>
    /// Specifies how a silo hosten in a service should find other nodes.
    /// </summary>
    public enum SiloClusterMode
    {
        /// <summary>
        /// Default. This value will be overwritten by a smart default as described on the other enum values.
        /// </summary>
        Unspecified,

        /// <summary>
        /// Specifies that this node is the primary node in a local cluster, and should host it's own in-memory
        /// membership and reminder tables. This is the default when running as either
        /// <see cref="ServiceStartupMode.CommandLineInteractive" /> or
        /// <see cref="ServiceStartupMode.CommandLineNonInteractive" /> or
        /// <see cref="ServiceStartupMode.VerifyConfigurations" />.
        /// </summary>
        PrimaryNode,

        /// <summary>
        /// Specifies that this node is a secondary node in a local cluster, and should contact a primary node for
        /// membership and reminder tables. This is not the default for any mode, and must be explicitly set. 
        /// </summary>
        SecondaryNode,

        /// <summary>
        /// Specifies that this node belongs to a real cluster, which has it's membership table managed by ZooKeeper and
        /// the reminder table stored on an external database. This is the default when running as
        /// <see cref="ServiceStartupMode.WindowsService" />.
        /// </summary>
        ZooKeeper
    }
}