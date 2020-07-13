using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Metrics.Logging;
using Metrics.MetricData;

namespace Metrics.Utils
{
    public static class AppEnvironment
    {
        private static readonly ILog log = LogProvider.GetCurrentClassLogger();

        public static IEnumerable<EnvironmentEntry> Current
        {
            get
            {
                yield return new EnvironmentEntry("MachineName", Environment.MachineName);
                yield return new EnvironmentEntry("DomainName", Environment.UserDomainName);
                yield return new EnvironmentEntry("UserName", Environment.UserName);
                yield return new EnvironmentEntry("ProcessName", SafeGetString(() => Process.GetCurrentProcess().ProcessName));
                yield return new EnvironmentEntry("OSVersion", Environment.OSVersion.ToString());
                yield return new EnvironmentEntry("CPUCount", Environment.ProcessorCount.ToString());
                yield return new EnvironmentEntry("CommandLine", Environment.CommandLine);
                yield return new EnvironmentEntry("HostName", SafeGetString(Dns.GetHostName));
                yield return new EnvironmentEntry("IPAddress", SafeGetString(GetIpAddress));
                yield return new EnvironmentEntry("LocalTime", Clock.FormatTimestamp(DateTime.Now));

                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    var entryAssemblyName = entryAssembly.GetName();
                    yield return new EnvironmentEntry("EntryAssemblyName", entryAssemblyName.Name);
                    yield return new EnvironmentEntry("EntryAssemblyVersion", entryAssemblyName.Version.ToString());
                }
            }
        }

        private static string GetIpAddress()
        {
            string hostName = SafeGetString(Dns.GetHostName);
            try
            {
                var ipAddress = Dns.GetHostEntry(hostName)
                    .AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

                if (ipAddress != null)
                {
                    return ipAddress.ToString();
                }
                return String.Empty;
            }
            catch (SocketException x)
            {
                if (x.SocketErrorCode == SocketError.HostNotFound)
                {
                    log.Warn(() => "Unable to resolve hostname " + hostName);
                    return String.Empty;
                }
                throw;
            }
        }

        private static string SafeGetString(Func<string> action)
        {
            try
            {
                return action();
            }
            catch (Exception x)
            {
                MetricsErrorHandler.Handle(x, "Error retrieving environment value");
                return String.Empty;
            }
        }

        /// <summary>
        /// Try to resolve Asp site name without compile-time linking System.Web assembly.
        /// </summary>
        /// <returns>Site name if able to identify</returns>
        public static string ResolveAspSiteName()
        {
            const string UnknownName = "UnknownSiteName";
            try
            {
                var runtimeType = Type.GetType("System.Web.HttpRuntime, System.Web, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false, true);
                if (runtimeType != null)
                {
                    var result = runtimeType.GetProperty("AppDomainAppVirtualPath").GetValue(null) as string;
                    if (result != null)
                    {
                        result = result.Trim('/');
                        if (result != String.Empty)
                        {
                            return result;
                        }

                        log.Debug("HttpRuntime.AppDomainAppVirtualPath is empty, trying AppDomainAppId");

                        result = runtimeType.GetProperty("AppDomainAppId").GetValue(null) as string;
                        if (result != null)
                        {
                            result = result.Trim('/');
                            if (result != String.Empty)
                            {
                                return result;
                            }
                        }
                        else
                        {
                            log.Warn("Unable to find property System.Web.HttpRuntime.AppDomainAppId to resolve AspSiteName");
                        }

                        log.Debug("HttpRuntime.AppDomainAppId is also empty, giving up trying to find site name");
                    }
                    else
                    {
                        log.Warn("Unable to find property System.Web.HttpRuntime.AppDomainAppVirtualPath to resolve AspSiteName");
                    }
                }
                else
                {
                    log.Warn("Unable to find type System.Web.HttpRuntime to resolve $Env.AppDomainAppVirtualPath$ macro");
                }
            }
            catch (Exception e)
            {
                log.WarnException("Unable to find type System.Web.HttpRuntime to resolve AspSiteName $Env.AppDomainAppVirtualPath$ macro", e);
            }

            return UnknownName;
        }
    }
}
