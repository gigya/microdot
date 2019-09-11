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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Exceptions;
using Newtonsoft.Json;

namespace Gigya.Microdot.Configuration
{
    ///<remarks>    
    ///If the environment variable "GIGYA_CONFIG_PATHS_FILE" is present, the path contained in it will be used. 
    ///If GIGYA_CONFIG_PATHS_FILE is not presented we will try to take folder from GIGYA_CONFIG_ROOT + loadPaths.json    
    ///If it not present as well it will be assumed that the list can be read from c:/gigya/config/loadPaths.json (by default). 
    ///In Linux, the path will be /etc/gigya/config/loadPaths.
    /// 
    ///Beware! This class is used during the logger initialization and shouldn't log anything at this stage.
    ///</remarks>
    public class ConfigurationLocationsParser: IConfigurationLocationsParser
    {
        internal const string GIGYA_CONFIG_PATHS_FILE = "GIGYA_CONFIG_PATHS_FILE";
        internal const string GIGYA_CONFIG_ROOT = "GIGYA_CONFIG_ROOT";
        internal const string GIGYA_CONFIG_ROOT_DEFAULT = "/gigya/config/";
        internal const string LOADPATHS_JSON = "loadPaths.json";
        private string AppName { get; }


        private class ErrorAggregator
        {
            public string Line { get; set; }
            public List<string> EnvVariables { get; set; } = new List<string>();
        }

        public string ConfigRoot { get; }
        public string LoadPathsFilePath { get; }

        public ConfigurationLocationsParser(IFileSystem fileSystemInstance, IEnvironmentVariableProvider environmentVariableProvider, CurrentApplicationInfo appInfo)
        {
            AppName = appInfo.Name;

            ConfigRoot = environmentVariableProvider.GetEnvironmentVariable(GIGYA_CONFIG_ROOT);

            if (string.IsNullOrEmpty(ConfigRoot))
                ConfigRoot = environmentVariableProvider.PlatformSpecificPathPrefix + GIGYA_CONFIG_ROOT_DEFAULT;

            LoadPathsFilePath = environmentVariableProvider.GetEnvironmentVariable(GIGYA_CONFIG_PATHS_FILE);
            
            if (string.IsNullOrEmpty(LoadPathsFilePath))
                LoadPathsFilePath = Path.Combine(ConfigRoot, LOADPATHS_JSON);

            //Normalize slashes
            LoadPathsFilePath = LoadPathsFilePath.Replace("\\", "/");

            Trace.WriteLine("Started parsing configurations from location " + LoadPathsFilePath +"\n");

            var configPathDeclarations = ParseAndValidateConfigLines(LoadPathsFilePath, fileSystemInstance);

            ConfigFileDeclarations = ExpandConfigPathDeclarations(environmentVariableProvider, configPathDeclarations, environmentVariableProvider.PlatformSpecificPathPrefix).ToArray();
        }


        private List<ConfigFileDeclaration> ExpandConfigPathDeclarations(IEnvironmentVariableProvider environmentVariableProvider, ConfigFileDeclaration[] configs, string prefix)
        {

            var configPathsSet = new SortedSet<ConfigFileDeclaration>(configs);

            var toSave = new List<ConfigFileDeclaration>();

            var listOfErrors = new List<ErrorAggregator>();

            foreach (var configPath in configPathsSet)
            {
                configPath.Pattern = configPath.Pattern.Replace("$(prefix)", prefix);

                var list = Regex.Matches(configPath.Pattern, "%([^%]+)%")
                                .Cast<Match>()
                                .Select(match => new
                                {
                                    Placehodler = match.Groups[0].Value,
                                    Value = environmentVariableProvider.GetEnvironmentVariable(match.Groups[1].Value)
                                }).ToList();

                var missingEnvVariables = list.Where(a => string.IsNullOrEmpty(a.Value)).Select(a => a.Placehodler.Trim('%')).ToList();

                if (missingEnvVariables.Any())
                {
                    listOfErrors.Add(new ErrorAggregator
                    {
                        Line = configPath.Pattern,
                        EnvVariables = missingEnvVariables
                    });
                }
                else
                {
                    foreach (var valToReplace in list)
                    {
                        configPath.Pattern = configPath.Pattern.Replace(valToReplace.Placehodler, valToReplace.Value);
                    }

                    configPath.Pattern = configPath.Pattern.Replace("\\", "/");
                    //Assumes $(appName) present only once.
                    if (Regex.Match(configPath.Pattern, @"/?[^$/]*\$\(appName\)").Success)
                        configPath.Pattern = configPath.Pattern.Replace("$(appName)", AppName);

                    toSave.Add(configPath);
                    Trace.WriteLine(configPath.Pattern + " priority=" + configPath.Priority + " " + SearchOption.TopDirectoryOnly);
                }
            }

            if (listOfErrors.Any())
            {
                var errorMessage = PrepareErrorMessage(listOfErrors);

                throw new ConfigurationException(errorMessage);
            }
            return toSave;
        }


        private static string PrepareErrorMessage(List<ErrorAggregator> notExistingEnvVariables)
        {
            string errorMessage = "Some environment variables are not defined, please add them.\n";

            foreach(var errorAggregator in notExistingEnvVariables)
            {
                var listOfVariables = string.Join(",", errorAggregator.EnvVariables.Distinct());
                errorMessage +=
                    $"Line: {errorAggregator.Line} missing valiables: {listOfVariables}\n";

            }
            return errorMessage;
        }


        private ConfigFileDeclaration[] ParseAndValidateConfigLines(string configPathFiles, IFileSystem fileSystemInstance)
        {
            ConfigFileDeclaration[] configs;

            try
            {
                var configsString = fileSystemInstance.ReadAllTextFromFile(LoadPathsFilePath);
                configs = JsonConvert.DeserializeObject<ConfigFileDeclaration[]>(configsString);
            }
            catch (Exception ex)
            {
                throw new ConfigurationException($"Problem reading {configPathFiles} file, {ex.InnerException}.", ex);
            }

            if (configs == null)
                return new ConfigFileDeclaration[0];

            var configLocationWithDuplicatePriority = configs.GroupBy(line => line.Priority).Where(priority => priority.Count() > 1).ToArray();

            if(configLocationWithDuplicatePriority.Any())
            {
                var message = new StringBuilder();
                message.AppendLine($"In {configPathFiles} some configurations lines have duplicate priorities.");
                foreach(var distinctPriority in configLocationWithDuplicatePriority)
                {
                    message.AppendLine($"Following locations share priority {distinctPriority.Key}:");
                    message.AppendLine(string.Join("\n", distinctPriority.Select(line => line.Pattern)));
                    message.AppendLine();
                }

                throw new EnvironmentException(message.ToString());
            }

            return configs;
        }

        //Stored in order of configuration read priority
        public IList<ConfigFileDeclaration> ConfigFileDeclarations { get; }


        public string TryGetFileFromConfigLocations(string fileName)
        {
            foreach (var configFileDeclaration in ConfigFileDeclarations)
            {
                var configDir = Path.GetDirectoryName(configFileDeclaration.Pattern);

                if (configDir == null || !Directory.Exists(configDir))
                    continue;

                try
                {
                    var filePath = Directory.GetFiles(configDir, fileName, SearchOption.TopDirectoryOnly).SingleOrDefault();

                    if (filePath != null)
                        return filePath;

                    string resDir = Path.Combine(configDir, "_res");
                    if (Directory.Exists(resDir))
                    {
                        filePath = Directory.GetFiles(resDir, fileName, SearchOption.TopDirectoryOnly).SingleOrDefault();

                        if (filePath != null)
                            return filePath;
                    }
                }
                catch (IOException) { }
            }

            return null;
        }

    }
}
