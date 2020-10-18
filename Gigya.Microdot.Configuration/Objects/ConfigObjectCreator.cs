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
using System.Text;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces;
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
        private DataAnnotationsValidator Validator { get; }
        private bool isCreated = false;

        private readonly AggregatingHealthStatus healthStatus;

        public ConfigObjectCreator(Type objectType, ConfigCache configCache, UsageTracking usageTracking, ILog log, Func<string, AggregatingHealthStatus> getAggregatedHealthCheck)
        {
            UsageTracking = usageTracking;
            Log = log;
            ObjectType = objectType;
            ConfigCache = configCache;
            ConfigPath = GetConfigPath();
            healthStatus = getAggregatedHealthCheck("Configuration");
            Validator = new DataAnnotationsValidator();

            Init();
        }

        private void Init()
        {
            Reload();
            ConfigCache.ConfigChanged.LinkTo(new ActionBlock<ConfigItemsCollection>(c => Reload()));
            InitializeBroadcast();
            healthStatus.Register(ObjectType.Name, HealthCheck);
        }

        private HealthMessage HealthCheck()
        {
            if (ValidationErrors != null)
            {
                return new HealthMessage(Health.Unhealthy, "The config object failed validation.\r\n" +
                                                   $"ConfigObjectType={ObjectType.FullName}\r\n" +
                                                   $"ConfigObjectPath={ConfigPath}\r\n" +
                                                   $"ValidationErrors={ValidationErrors}");
            }

            return new HealthMessage(Health.Healthy, "OK");
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
					// Pay attention, at least the ConfigurationVerificator class relying on these keys to extract details of validation errors during the load.
                    { "ConfigObjectType", ObjectType.FullName },
                    { "ConfigObjectPath", ConfigPath },
                    { "ValidationErrors", ValidationErrors }
                });
            }

            return Latest;
        }

        public Func<T> GetTypedLatestFunc<T>() where T : class => () => GetLatest() as T;
        public Func<T> GetChangeNotificationsFunc<T>() where T : class => () => ChangeNotifications as T;

        public static bool IsConfigObject(Type service)
        {
            return service.IsClass && !service.IsAbstract && !service.IsInterface && typeof(IConfigObject).IsAssignableFrom(service);
        }
        
        public dynamic GetLambdaOfGetLatest(Type configType)
        {
            return GetGenericFuncCompiledLambda(configType, nameof(GetTypedLatestFunc));
        }

        public dynamic GetLambdaOfChangeNotifications(Type configType)
        {
            return GetGenericFuncCompiledLambda(configType, nameof(GetChangeNotificationsFunc));
        }

        private dynamic GetGenericFuncCompiledLambda(Type configType, string functionName)
        {//happens only once while loading, but can be optimized by creating Method info before sending to this function, if needed
            MethodInfo func = typeof(ConfigObjectCreator).GetMethod(functionName).MakeGenericMethod(configType);
            Expression instance = Expression.Constant(this);
            Expression callMethod = Expression.Call(instance, func);
            Type delegateType = typeof(Func<>).MakeGenericType(configType);
            Type parentExpressionType = typeof(Func<>).MakeGenericType(delegateType);

            dynamic lambda = Expression.Lambda(parentExpressionType, callMethod).Compile();

            return lambda;
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

        public void Reload()
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
                try
                {
                    updatedConfig = config.ToObject(ObjectType);
                }
                catch (Exception ex)
                {
                    // It is not only JsonException, as sometimes a custom deserializer capable to throw god knows what (including ProgrammaticException)
                    errors.Add(new ValidationResult("Failed to deserialize config object: " + HealthMonitor.GetMessages(ex)));
                }

                if (updatedConfig != null)
                    Validator.TryValidateObjectRecursive(updatedConfig, errors);
            }

            if (errors.Any() == false)
            {
                ValidationErrors = null;
                UsageTracking.AddConfigObject(Latest, ConfigPath);
                if (isCreated)
                {
                    Log.Info(_ => _("A config object has been updated",
                        unencryptedTags: new {
                            ConfigObjectType  = ObjectType.FullName,
                            ConfigObjectPath  = ConfigPath,
                            OverallModifyTime = ConfigCache.LatestConfigFileModifyTime,
                        },
                        encryptedTags: new {
                            Changes = DiffJObjects(LatestNode, config, new StringBuilder(), new Stack<string>()).ToString(),
                        }));
                }
                else//It mean we are first time not need to send update messsage 
                {
                    isCreated = true;
                }

                LatestNode = config;
                Latest = updatedConfig;
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


        static StringBuilder DiffJObjects(JObject left, JObject right, StringBuilder sb, Stack<string> path)
        {
            var leftEnum = ((IEnumerable<KeyValuePair<string, JToken>>)left).OrderBy(_ => _.Key).GetEnumerator();
            var rightEnum = ((IEnumerable<KeyValuePair<string, JToken>>)right).OrderBy(_ => _.Key).GetEnumerator();
            bool moreLeft = leftEnum.MoveNext();
            bool moreRight = rightEnum.MoveNext();
            while (moreLeft || moreRight)
            {
                if (moreLeft && (!moreRight || leftEnum.Current.Key.CompareTo(rightEnum.Current.Key) < 0))
                {
                    sb.Append(string.Join(".", path.Append(leftEnum.Current.Key))).Append(":\t").Append(JsonConvert.SerializeObject(leftEnum.Current.Value)).AppendLine("\t-->\tnull");
                    moreLeft = leftEnum.MoveNext();
                }
                else if (moreRight && (!moreLeft || leftEnum.Current.Key.CompareTo(rightEnum.Current.Key) > 0))
                {
                    sb.Append(string.Join(".", path.Append(rightEnum.Current.Key))).Append(":\tnull\t-->\t").AppendLine(JsonConvert.SerializeObject(rightEnum.Current.Value));
                    moreRight = rightEnum.MoveNext();
                }
                else if (leftEnum.Current.Value.Type != rightEnum.Current.Value.Type)
                {
                    sb.Append(string.Join(".", path.Append(leftEnum.Current.Key))).Append(":\t").Append(JsonConvert.SerializeObject(leftEnum.Current.Value))
                        .Append("\t-->\t").AppendLine(JsonConvert.SerializeObject(rightEnum.Current.Value));
                    moreLeft = leftEnum.MoveNext();
                    moreRight = rightEnum.MoveNext();
                }
                else if (leftEnum.Current.Value.Type != JTokenType.Object)
                {
                    if (!JToken.DeepEquals(leftEnum.Current.Value, rightEnum.Current.Value))
                        sb.Append(string.Join(".", path.Append(leftEnum.Current.Key))).Append(":\t").Append(JsonConvert.SerializeObject(leftEnum.Current.Value))
                            .Append("\t-->\t").AppendLine(JsonConvert.SerializeObject(rightEnum.Current.Value));
                    moreLeft = leftEnum.MoveNext();
                    moreRight = rightEnum.MoveNext();
                }
                else
                {
                    path.Push(leftEnum.Current.Key);
                    DiffJObjects((JObject)leftEnum.Current.Value, (JObject)rightEnum.Current.Value, sb, path);
                    path.Pop();
                    moreLeft = leftEnum.MoveNext();
                    moreRight = rightEnum.MoveNext();
                }
            }

            return sb;
        }
    }
}