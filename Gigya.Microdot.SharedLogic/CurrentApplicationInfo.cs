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
using System.Net;
using System.Reflection;
using System.Security.Principal;

namespace Gigya.Microdot.SharedLogic {

	/// <summary>
	/// Provides info about the current application.
	/// </summary>
	public static class CurrentApplicationInfo
	{
	    public const string DEFAULT_INSTANCE_NAME = "DefaultInstance";

		/// <summary>Application/system/micro-service name, as provided by developer.</summary>
		public static string Name { get; private set; }

		/// <summary>Logical instance name for the current application, which can be used to differentiate between
		/// multiple identical applications running on the same host.</summary>
		public static string InstanceName { get; private set; }

		/// <summary>The name of the operating system user that runs this process.</summary>
		public static string OsUser { get; }

		/// <summary>The application version.</summary>
		public static Version Version { get; }

        /// <summary>The Infrastructure version.</summary>
        public static Version InfraVersion { get; private set; }

        /// <summary>
        /// Name of host, the current process is runing on.
        /// </summary>
        public static string HostName { get; }

		/// <summary>
		/// Indicates if the current process is running as a Windows service.
		/// </summary>
		public static bool IsRunningAsWindowsService { get; }

        public static bool HasConsoleWindow { get; }

	    static CurrentApplicationInfo()
		{
            OsUser = WindowsIdentity.GetCurrent().Name;
		    InfraVersion = typeof(CurrentApplicationInfo).Assembly.GetName().Version;
            Version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
			HostName = Dns.GetHostName();
			// ReSharper disable once PossibleNullReferenceException
			IsRunningAsWindowsService = Environment.OSVersion.Platform == PlatformID.Win32NT &&
                WindowsIdentity.GetCurrent().Name == @"NT AUTHORITY\SYSTEM";
		    HasConsoleWindow = !IsRunningAsWindowsService && !Console.IsInputRedirected;
        }

		public static void Init(string name, string instanceName = null, Version infraVersion = null )
		{
		    
		    if (name == null)
		        throw new ArgumentNullException(nameof(name));
            Name = name;

			InfraVersion = infraVersion ?? typeof(CurrentApplicationInfo).Assembly.GetName().Version;
			InstanceName = instanceName
		                   ?? Environment.GetEnvironmentVariable("GIGYA_SERVICE_INSTANCE_NAME")
		                   ?? DEFAULT_INSTANCE_NAME;

            if (string.IsNullOrEmpty(InstanceName))
                throw new ArgumentNullException(nameof(instanceName));
		}
	}
}
