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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Text;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Newtonsoft.Json;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public class ConfigurationResponseBuilder
    {
        readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented};

        private UsageTracking UsageTracking { get; }
        private ServiceArguments ServiceArguments { get; }
        public CurrentApplicationInfo AppInfo { get; }
        private ConfigCache ConfigCache { get; }
        private IEnvironment Envs { get; }
        private IAssemblyProvider AssemblyProvider { get; }

        public ConfigurationResponseBuilder(ConfigCache configCache,
                                            IEnvironment envs,
                                            IAssemblyProvider assemblyProvider,
                                            UsageTracking usageTracking,
                                            ServiceArguments serviceArguments,
                                            CurrentApplicationInfo appInfo)
        {
            UsageTracking = usageTracking;
            ServiceArguments = serviceArguments;
            AppInfo = appInfo;
            ConfigCache = configCache;
            Envs = envs;
            AssemblyProvider = assemblyProvider;
        }


        public string BuildJson()
        {
            var jsonObject = new
            {
                Hashes = GetHashes(),
                EnvironmentVariables = GetEnvironmentVariables(),
                AssemblyVersions = GetAssemblyVersions(),
                RuntimeInfo = GetRuntimeInfo(),
                ServiceArguments = GetServiceArguments(),
                ConfigurationEntries = GetConfigurationEntries()
            };

            return JsonConvert.SerializeObject(jsonObject, JsonSettings);
        }


        public string BuildText()
        {
            var sb = new StringBuilder();
            var hashes = GetHashes();
            var envVars = GetEnvironmentVariables();
            var maxNameLen = envVars.Keys.Select(k => k.Length).Max();

            sb.AppendLine($"===   Hash of everything: {hashes["Everything"]}     ===\n");


            sb.AppendLine($"\n\n\n===   Environment Variables (hash {hashes["EnvironmentVariables"]})     ===\n");

            foreach (var envVar in envVars)
                sb.AppendLine($"{envVar.Key.PadRight(maxNameLen)} = {envVar.Value}");


            var assemblyVersions = GetAssemblyVersions();
            maxNameLen = assemblyVersions.Keys.Select(k => k.Length).Max();

            sb.AppendLine($"\n\n\n===     Assembly Versions (hash {hashes["AssemblyVersions"]})     ===\n");

            foreach (var assemblyVersion in assemblyVersions)
                sb.AppendLine($"{assemblyVersion.Key.PadRight(maxNameLen)} {assemblyVersion.Value}");


            var runtimeInfo = GetRuntimeInfo();
            maxNameLen = runtimeInfo.Keys.Select(k => k.Length).Max();

            sb.AppendLine($"\n\n\n===       Runtime Info (hash {hashes["RuntimeInfo"]})        ===\n");

            foreach (var info in runtimeInfo)
                sb.AppendLine($"{info.Key.PadRight(maxNameLen)} = {info.Value}");


            var serviceArguments = GetServiceArguments();
            maxNameLen = serviceArguments.Keys.Select(k => k.Length).Max();

            sb.AppendLine($"\n\n\n===       Service Arguments (hash {hashes["ServiceArguments"]})        ===\n");

            foreach (var info in serviceArguments)
                sb.AppendLine($"{info.Key.PadRight(maxNameLen)} = {info.Value}");


            sb.AppendLine($"\n\n\n===   Configuration Entries (hash {hashes["ConfigurationEntries"]})   ===\n");

            var configItems = GetConfigurationEntries();
            foreach (var configItem in configItems)
            {
                sb.AppendLine($"{configItem.Key} = {ToCSharpStringLiteral(configItem.Value.Value)} [{configItem.Value.File}] [{(configItem.Value.UsedAs != null ? "USED" : "UNUSED")}]");

                foreach (var overrideInfo in configItem.Value.Overridden ?? new ConfigEntry[0])
                    sb.AppendLine($"{"".PadRight(configItem.Key.Length - 3)} ~~~> {ToCSharpStringLiteral(overrideInfo.Value)} [{overrideInfo.File}] [{(overrideInfo.UsedAs != null ? "USED" : "UNUSED")}]");
            }

            return sb.ToString();
        }


        private Dictionary<string, int> GetHashes()
        {
            var env = JsonConvert.SerializeObject(GetEnvironmentVariables()).GetHashCode();
            var ver = JsonConvert.SerializeObject(GetAssemblyVersions()).GetHashCode();
            var runtime = JsonConvert.SerializeObject(GetRuntimeInfo()).GetHashCode();
            var arguments = JsonConvert.SerializeObject(GetServiceArguments()).GetHashCode();
            var config = JsonConvert.SerializeObject(GetConfigurationEntries()).GetHashCode();
            var all = (((env * 397 ^ ver) * 397 ^ runtime) * 397 ^ arguments) * 397 ^ config;

            return new Dictionary<string, int>
            {
                { "Everything", all },
                { "EnvironmentVariables", env },
                { "AssemblyVersions", ver },
                { "RuntimeInfo", runtime },
                { "ServiceArguments", arguments },
                { "ConfigurationEntries", config }
            };
        }

        private Dictionary<string, string> GetEnvironmentVariables()
        {
            return Environment.GetEnvironmentVariables()
                       .OfType<DictionaryEntry>()
                       .Select(x => new { Name = (string)x.Key, Value = (string)x.Value })
                       .Where(x => x.Name.ToUpper() == "DC" || x.Name.ToUpper() == "ZONE" || x.Name.ToUpper() == "REGION" || x.Name.ToUpper() == "ENV" || x.Name.ToUpper().Contains("GIGYA"))
                       .OrderBy(x => x.Name)
                       .ToDictionary(x => x.Name, x => x.Value);
        }

        private Dictionary<string, string> GetAssemblyVersions()
        {
            var specialVersions = new[] { new { Name = "(service)", Version = GetVersion(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()) } };
            var assemblyVersions = AssemblyProvider.GetAssemblies()
                                                   .Where(x => x.GlobalAssemblyCache == false)
                                                   .Select(a => new { a.GetName().Name, Version = GetVersion(a) });

            return specialVersions
                .Concat(assemblyVersions)
                .OrderBy(x => x.Name).GroupBy(a => a.Name)
                .ToDictionary(a => a.Key, a => a.First().Version);
        }


        private string GetVersion(Assembly assembly)
        {
            string assemblyVersion = assembly.GetName().Version.ToString();

            try
            {
                string productVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (productVersion != null && assemblyVersion != productVersion && assemblyVersion.StartsWith(productVersion) == false)
                    return $"{assemblyVersion} ({productVersion})";
            }
            catch
            {
                // Ignore, best effort. GetCustomAttribute() can sometimes throw FileNotFoundException or other exceptions when it
                // needs to load additional assemblies to resolve the attribute.
            }

            return assemblyVersion;
        }


        private Dictionary<string, string> GetRuntimeInfo()
        {
            return new Dictionary<string, string>
            {
                { "ApplicationName", AppInfo.Name },
                { "HostName", CurrentApplicationInfo.HostName},
                { "InstanceName", Envs.InstanceName },
                { "OSUser", AppInfo.OsUser },
                { "OSVersion", Environment.OSVersion.ToString() },
                { "CommandLine", Environment.CommandLine },
                { "CurrentDirectory", Environment.CurrentDirectory },
                { "Is64BitOperatingSystem", Environment.Is64BitOperatingSystem.ToString() },
                { "Is64BitProcess", Environment.Is64BitProcess.ToString() },
                { "ProcessorCount", Environment.ProcessorCount.ToString() },
                { "UserInteractive", Environment.UserInteractive.ToString() },
                { "ClrVersion", Environment.Version.ToString() },
                { "CurrentProcessId", Process.GetCurrentProcess().Id.ToString() },
                { "CurrentCulture", CultureInfo.CurrentCulture.ToString() },
                { "CurrentUICulture", CultureInfo.CurrentUICulture.ToString() },
                { "IsServerGC", GCSettings.IsServerGC.ToString() },
                { "LatencyMode", GCSettings.LatencyMode.ToString() },
                { "LargeObjectHeapCompactionMode", GCSettings.LargeObjectHeapCompactionMode.ToString() },
                { "Expect100Continue", ServicePointManager.Expect100Continue.ToString() },
                { "UseNagleAlgorithm", ServicePointManager.UseNagleAlgorithm.ToString() },
                { "DefaultConnectionLimit", ServicePointManager.DefaultConnectionLimit.ToString() },
                { "SecurityProtocol", ServicePointManager.SecurityProtocol.ToString() }
            };
        }

        private Dictionary<string, string> GetServiceArguments()
        {
            var dict = new Dictionary<string, string>();

            foreach (var property in typeof(ServiceArguments).GetProperties())
            {
                var value = property.GetValue(ServiceArguments);

                if (value != null && property.PropertyType.IsArray)
                {
                    var untypedArray = (Array)value;
                    var typedArray = new object[untypedArray.Length];
                    untypedArray.CopyTo(typedArray, 0);
                    dict[property.Name] = string.Join(",", typedArray);
                }
                else
                {
                    dict[property.Name] = value?.ToString() ?? "<null>";
                }
            }

            return dict;
        }


        private Dictionary<string, ConfigEntry> GetConfigurationEntries()
        {
            var dictionary = new Dictionary<string, ConfigEntry>();

            foreach (var configItem in ConfigCache.LatestConfig.Items.OrderBy(item => item.Key))
            {
                var overrides = configItem.Overrides.Select(e => new ConfigEntry { Value = e.Value, File = e.FileName }).Skip(1).ToArray();
                if (!overrides.Any())
                    overrides = null;

                var configItemInfo = configItem.Overrides.First();
                dictionary.Add(configItem.Key, new ConfigEntry
                {
                    Value = configItemInfo.Value,
                    File = configItemInfo.FileName,
                    Overridden = overrides,
                    UsedAs = UsageTracking.Get(configItem.Key)?.FullName
                });
            }

            return dictionary;
        }


        private class ConfigEntry
        {
            public string Value;
            public string File;
            public IEnumerable<ConfigEntry> Overridden;
            public string UsedAs;
        }


        private static string ToCSharpStringLiteral(string input)
        {
            StringBuilder literal = new StringBuilder(input.Length + 2);
            literal.Append("\"");
            foreach (var c in input)
            {
                switch (c)
                {
                    case '\'': literal.Append(@"\'"); break;
                    case '\"': literal.Append("\\\""); break;
                    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        // ASCII printable character
                        if (c >= 0x20 && c <= 0x7e)
                        {
                            literal.Append(c);
                            // As UTF16 escaped character
                        }
                        else
                        {
                            literal.Append(@"\u");
                            literal.Append(((int)c).ToString("x4"));
                        }
                        break;
                }
            }
            literal.Append("\"");
            return literal.ToString();
        }

    }
}
