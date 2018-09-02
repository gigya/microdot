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
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Configuration.Objects
{
    public class ConfigObjectCreator : IConfigObjectCreator
    {

        /// <summary>
        /// Gets an ISourceBlock the provides notifications of config changes. This value is cached for quick retrieval.
        /// </summary>
        public object ChangeNotifications { get; private set; }

        private object Latest { get; set; }
        private UsageTracking UsageTracking { get; }
        private ILog Log { get; }
        private Type ObjectType { get; }
        private ConfigCache ConfigCache { get; }
        private string ConfigPath { get; }
        private Action<object> SendChangeNotification { get; set; }
        private string ValidationErrors { get; set; }
        private JObject LatestNode { get; set; }
        private JObject Empty { get; } = new JObject();
        private DataAnnotationsValidator.DataAnnotationsValidator Validator { get; }

        private readonly AggregatingHealthStatus healthStatus;

        public ConfigObjectCreator(Type objectType, ConfigCache configCache, UsageTracking usageTracking, ILog log, Func<string, AggregatingHealthStatus> getAggregatedHealthCheck)
        {
            UsageTracking = usageTracking;
            Log = log;
            ObjectType = objectType;
            ConfigCache = configCache;
            ConfigPath = GetConfigPath();
            healthStatus = getAggregatedHealthCheck("Configuration");
            Validator = new DataAnnotationsValidator.DataAnnotationsValidator();
        }

        public void Init()
        {
            Create();
            ConfigCache.ConfigChanged.LinkTo(new ActionBlock<ConfigItemsCollection>(c => Create()));
            InitializeBroadcast();
            healthStatus.RegisterCheck(ObjectType.Name, HealthCheck);
        }

        private HealthCheckResult HealthCheck()
        {
            if (ValidationErrors != null)
            {
                return HealthCheckResult.Unhealthy("The config object failed validation.\r\n" +
                                                   $"ConfigObjectType={ObjectType.FullName}\r\n" +
                                                   $"ConfigObjectPath={ConfigPath}\r\n" +
                                                   $"ValidationErrors={ValidationErrors}");
            }

            return HealthCheckResult.Healthy();
        }

        /// <summary>
        /// Gets the latest version of the configuration. This value is cached for quick retrieval. If the config object
        /// fails validation, an exception will be thrown.
        /// </summary>
        /// <exception cref="ConfigurationException">When the configuration fails validation, this exception will be
        /// thrown, with details regarding that has failed.</exception>
        public object GetLatest()
        {
            if (Latest == null)
            {
                throw new ConfigurationException("The config object failed validation.", unencrypted: new Tags
                {
                    { "ConfigObjectType", ObjectType.FullName },
                    { "ConfigObjectPath", ConfigPath },
                    { "ValidationErrors", ValidationErrors }
                });
            }

            return Latest;
        }

        public static bool IsConfigObject(Type service)
        {
            return service.IsClass && !service.IsAbstract && typeof(IConfigObject).IsAssignableFrom(service);
        }

        private string GetConfigPath()
        {
            var configPath = ObjectType.Name;

            var rootAttribute = ObjectType.GetCustomAttribute<ConfigurationRootAttribute>();
            if (rootAttribute != null)
            {
                configPath = rootAttribute.Path;
                if (rootAttribute.BuildingStrategy == RootStrategy.AppendClassNameToPath)
                {
                    configPath = configPath + "." + ObjectType.Name;
                }
            }

            return configPath;
        }

        private void InitializeBroadcast()
        {
            var broadcastBlockType = typeof(BroadcastBlock<>).MakeGenericType(ObjectType);

            ChangeNotifications = Activator.CreateInstance(broadcastBlockType, new object[] { null });

            var broadcastBlockConst = Expression.Constant(ChangeNotifications);
            var convertedBlock = Expression.Convert(broadcastBlockConst, broadcastBlockType);

            var configParam = Expression.Parameter(typeof(object), "updatedConfig");
            var convertedConfig = Expression.Convert(configParam, ObjectType);

            var postMethod = typeof(DataflowBlock).GetMethod("Post").MakeGenericMethod(ObjectType);
            var postCall = Expression.Call(postMethod, convertedBlock, convertedConfig);
            var lambda = Expression.Lambda<Action<object>>(postCall, configParam);

            SendChangeNotification = lambda.Compile();
        }

        private void Create()
        {
            var errors = new List<ValidationResult>();
            JObject config = null;
            object updatedConfig = null;

            try
            {
                config = ConfigCache.CreateJsonConfig(ConfigPath) ?? Empty;

                if(JToken.DeepEquals(LatestNode, config))
                {
                    if (Latest != null)
                        ValidationErrors = null;

                    return;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationResult("Failed to acquire config JObject: " + HealthMonitor.GetMessages(ex)));
            }

            if (config != null && errors.Any() == false)
            {
                LatestNode = config;

                try
                {
                    updatedConfig = LatestNode.ToObject(ObjectType);
                }
                catch (JsonException ex)
                {
                    errors.Add(new ValidationResult("Failed to deserialize config object: " + HealthMonitor.GetMessages(ex)));
                }

                if (updatedConfig != null)
                    Validator.TryValidateObjectRecursive(updatedConfig, errors);
            }

            if (errors.Any() == false)
            {
                Latest = updatedConfig;
                ValidationErrors = null;
                UsageTracking.AddConfigObject(Latest, ConfigPath);

                Log.Info(_ => _("A config object has been updated", unencryptedTags: new
                {
                    ConfigObjectType = ObjectType.FullName,
                    ConfigObjectPath = ConfigPath
                }));

                SendChangeNotification?.Invoke(Latest);
            }
            else
            {
                ValidationErrors = string.Join(" \n", errors.Select(a => a.ErrorMessage));

                Log.Error(_ => _("A config object has been updated but failed validation", unencryptedTags: new
                {
                    ConfigObjectType = ObjectType.FullName,
                    ConfigObjectPath = ConfigPath,
                    ValidationErrors
                }));
            }
        }
    }
}