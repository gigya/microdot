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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.LanguageExtensions;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Utils;

namespace Gigya.Microdot.Hosting.Environment
{
    // TODO: This should not be part of Microdot
    [ConfigurationRoot("dataCenters", RootStrategy.ReplaceClassNameWithPath)]
    public class DataCentersConfig : IConfigObject
    {
        public string Current { get; set; }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HostEnvironment : IEnvironment
    {
        private const string GIGYA_CONFIG_ROOT_DEFAULT = "config";
        private const string LOADPATHS_JSON = "loadPaths.json";

        private const string GIGYA_CONFIG_ROOT_KEY = "GIGYA_CONFIG_ROOT";
        private const string GIGYA_CONFIG_PATHS_FILE_KEY = "GIGYA_CONFIG_PATHS_FILE";

        // TODO: Add doc
        public HostEnvironment(params IHostEnvironmentSource[] sources)
            : this(sources as IEnumerable<IHostEnvironmentSource>) { }

        public HostEnvironment(IEnumerable<IHostEnvironmentSource> sources)
        {
            environmentVariables = new Dictionary<string, string>();

            foreach (var s in sources)
            {
                Zone                  = pipeParameter(nameof(Zone),                  Zone,                  s.Zone);
                Region                = pipeParameter(nameof(Region),                Region,                s.Region);
                DeploymentEnvironment = pipeParameter(nameof(DeploymentEnvironment), DeploymentEnvironment, s.DeploymentEnvironment);
                ConsulAddress         = pipeParameter(nameof(ConsulAddress),         ConsulAddress,         s.ConsulAddress);
                ApplicationInfo       = pipeParameter(nameof(ApplicationInfo),       ApplicationInfo,       s.ApplicationInfo);
                InstanceName          = pipeParameter(nameof(InstanceName),          InstanceName,          s.InstanceName);
                ConfigRoot            = pipeFsiParameter(nameof(ConfigRoot),         ConfigRoot,            s.ConfigRoot);
                LoadPathsFile         = pipeFsiParameter(nameof(LoadPathsFile),      LoadPathsFile,         s.LoadPathsFile);

                consumeCustomKeys(s);
            }

            if (Zone == null) throw MakeException(nameof(Zone));
            if (DeploymentEnvironment == null) throw MakeException(nameof(DeploymentEnvironment));
            if (ConsulAddress == null) throw MakeException(nameof(ConsulAddress));
            if (ApplicationInfo == null) throw new ArgumentNullException(nameof(ApplicationInfo));

            InstanceName ??= "DefaultInstance";
            ConfigRoot ??= GetDefaultConfigRoot();
            LoadPathsFile ??= GetDefaultPathsFile();

            // TODO: Fix error messages.
            if (ConfigRoot.Exists == false)
            {
                throw new EnvironmentException(
                    $"ConfigRoot path doesn't exist '{ ConfigRoot.FullName }'. " +
                    $"Use '{GIGYA_CONFIG_ROOT_KEY}' environment variable to override default path.");
            }

            if (LoadPathsFile.Exists == false)
            {
                throw new EnvironmentException(
                    $"LoadPaths file isn't found at '{ LoadPathsFile.FullName }'. " +
                    $"Use '{GIGYA_CONFIG_PATHS_FILE_KEY}' environment variable to define absolute path" +
                    $"to the file or place a 'loadPaths.json' at your config root.");
            }
            

            void consumeCustomKeys(IHostEnvironmentSource cs)
            {
                foreach (var k in cs.EnvironmentVariables)
                {
                    if (k.Value != null)
                        environmentVariables[k.Key] = k.Value;
                }
            }

            T pipeFsiParameter<T>(string name, T orig, T @new)
                where T : FileSystemInfo
            {
                if (orig == null && @new != null)
                    Trace.WriteLine($"HostConfiguration: Setting { name } <- {@new.FullName}");

                if (orig != null && @new != null)
                    Trace.WriteLine($"HostConfiguration: Overriding { name } <- { @new.FullName }");

                return @new ?? orig;
            }

            T pipeParameter<T>(string name, T orig, T @new)
            {
                if (orig == null && @new != null)
                    Trace.WriteLine($"HostConfiguration: Setting { name } <- {@new}");

                if (orig != null && @new != null)
                    Trace.WriteLine($"HostConfiguration: Overriding { name } <- { @new }");

                return @new ?? orig;
            }

            Exception MakeException(string arg)
            {
                throw new ArgumentNullException(
                    $"{ arg } environement variable wasn't supplied. If you're using " +
                    $"local variables file make sure it's path is defined under " +
                    $"GIGYA_ENVVARS_FILE environemnt variable.");
            }
        }

        private DirectoryInfo GetDefaultConfigRoot() =>
            Path.Combine(System.Environment.CurrentDirectory, GIGYA_CONFIG_ROOT_DEFAULT)
                .To(x => new DirectoryInfo(x));

        private FileInfo GetDefaultPathsFile() =>
            Path.Combine(ConfigRoot.FullName, LOADPATHS_JSON)
                .To(x => new FileInfo(x));


        public string InstanceName { get; }

        public string Zone { get; }
        public string Region { get; }
        public string DeploymentEnvironment { get; }
        public string ConsulAddress { get; }
        public DirectoryInfo ConfigRoot { get; }
        public FileInfo LoadPathsFile { get; }
        public CurrentApplicationInfo ApplicationInfo { get; }


        private readonly Dictionary<string, string> environmentVariables;

        public string this[string key]
        {
            get
            {
                if (environmentVariables.TryGetValue(key, out var val))
                    return val;
                return null;
            }
        }

        public static HostEnvironment CreateDefaultEnvironment(string serviceName, Version infraVersion, ServiceArguments arguments = null)
        {
            return new HostEnvironment(GetDefaultSources(serviceName, infraVersion, arguments));
        }

        public static IEnumerable<IHostEnvironmentSource> GetDefaultSources(string serviceName, Version infraVersion, ServiceArguments arguments = null)
        {
            var l = new List<IHostEnvironmentSource>(3);

            l.Add(new EnvironmentVarialbesConfigurationSource());

            if (System.Environment.GetEnvironmentVariable("GIGYA_ENVVARS_FILE") is string path)
            {
                l.Add(new LegacyFileHostConfigurationSource(path));
            }

            else if (File.Exists(@"D:\gigya\environmentVariables.json"))
            {
                l.Add(new LegacyFileHostConfigurationSource(@"D:\gigya\environmentVariables.json"));
            }

            if (arguments != null)
            {
                l.Add(new FreeHostEnvironmentSource(
                    instanceName: arguments.InstanceName));
            }

            l.Add(
                new ApplicationInfoSource(
                    new CurrentApplicationInfo(
                        serviceName,
                        System.Environment.UserName,
                        System.Net.Dns.GetHostName(),
                        infraVersion: infraVersion)));

            return l;
        }

    }
}
