using Gigya.Microdot.LanguageExtensions;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Gigya.Microdot.SharedLogic
{
    /// <summary>
    /// Provides info about the current application.
    /// </summary>
    /// <remarks>
    /// I think we should use this class only for init IEnvironment the get should be in IEnvironment it simply the code 
    /// </remarks>
    public class CurrentApplicationInfo
    {
        /// <summary>Application/system/micro-service name, as provided by developer.</summary>
        public string Name { get; }

        /// <summary>The name of the operating system user that runs this process.</summary>
        public string OsUser { get; }

        /// <summary>The application version.</summary>
        public Version Version { get; }

        /// <summary>The Infrastructure version.</summary>
        public Version InfraVersion { get; }

        /// <summary>Is this Linux</summary>
        public static bool IsLinux = false;

        /// <summary>
        /// Name of host, the current process is running on.
        /// </summary>
        public static string HostName { get; private set; }
        public static string ContainerParentName { get; private set; }

        /// <summary>
        /// Indicates whether current process has an interactive console window.
        /// </summary>
        public bool HasConsoleWindow { get; }

        /// <summary>
        /// Waring take it form IEnvironment
        /// </summary>
        internal string InstanceName { get; }

        public CurrentApplicationInfo(string name, string instanceName = null, Version infraVersion = null)
            : this(name, Environment.UserName, System.Net.Dns.GetHostName(), instanceName, infraVersion, containerParentName: null)
        { }

        public CurrentApplicationInfo(
            string name,
            string osUser,
            string hostName,
            string instanceName = null, 
            Version infraVersion = null,
            string containerParentName = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            OsUser = osUser.NullWhenEmpty() ?? throw new ArgumentNullException(nameof(osUser));
            HostName = hostName.NullWhenEmpty() ?? throw new ArgumentNullException(nameof(hostName));

            Version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;

            // TODO: Consider using Environment.UserInteractive
            HasConsoleWindow = !Console.IsInputRedirected;

            // TODO: Possible error: Microdot version assigned to Infra version
            InfraVersion = infraVersion ?? typeof(CurrentApplicationInfo).Assembly.GetName().Version;

            InstanceName = instanceName;
            ContainerParentName = containerParentName;
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }
    }
}
