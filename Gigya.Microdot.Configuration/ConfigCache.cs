﻿#region Copyright 
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
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Interfaces.Logging;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Configuration
{
    public class ConfigCache
    {
        public ISourceBlock<ConfigItemsCollection> ConfigChanged => ConfigChangedBroadcastBlock;
        public ConfigItemsCollection LatestConfig { get; private set; }

        private ILog Log { get; }

        private IConfigItemsSource Source { get; }
        private BroadcastBlock<ConfigItemsCollection> ConfigChangedBroadcastBlock { get; }

        public ConfigCache(IConfigItemsSource source, IConfigurationDataWatcher watcher,ILog log)
        {
            Source = source;
            Log = log;
            ConfigChangedBroadcastBlock = new BroadcastBlock<ConfigItemsCollection>(null);

            watcher.DataChanges.LinkTo(new ActionBlock<bool>(nothing => Refresh()));

            Refresh(false).GetAwaiter().GetResult();
        }


        private async Task Refresh(bool catchExceptions=true)
        {
            //Prevents faulting of action block
            try
            {
                LatestConfig = await Source.GetConfiguration().ConfigureAwait(false);
                ConfigChangedBroadcastBlock.Post(LatestConfig);
            }
            catch(Exception ex) when(catchExceptions)
            {
                Log.Warn("Error with Refreshing configuration.", exception: ex);
            }                
        }


        public JObject CreateJsonConfig(string path)
        {
            path = path + ".";
            var root = new JObject();

            foreach (var configItem in LatestConfig.Items.Where(kvp => kvp.Key.StartsWith(path,StringComparison.OrdinalIgnoreCase)))
            {
                var pathParts = configItem.Key.Substring(path.Length).Split('.');
                var currentObj = root;

                foreach (string pathPart in pathParts.Take(pathParts.Length - 1))
                {
                    var existing = currentObj.GetValue(pathPart, StringComparison.OrdinalIgnoreCase);

                    if (existing == null || existing is JObject == false)
                    {
                        existing = new JObject();
                        currentObj[pathPart] = existing;
                    }

                    currentObj = (JObject)existing;
                }

                currentObj[pathParts.Last()] = configItem.Value;
            }

            return root;
        }
    }
}
