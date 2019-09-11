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
using System.Threading.Tasks;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Fakes
{
    public class OverridableConfigItems : IConfigItemsSource
    {
        private readonly ConfigDecryptor _configDecryptor;
        private Dictionary<string, string> Data { get; }

        private FileBasedConfigItemsSource FileBasedConfigItemsSource { get; }

        public OverridableConfigItems(FileBasedConfigItemsSource fileBasedConfigItemsSource,
                                        Dictionary<string, string> data, ConfigDecryptor configDecryptor)
        {
            _configDecryptor = configDecryptor;
            FileBasedConfigItemsSource = fileBasedConfigItemsSource;
            Data = data;
        }


        public async Task<ConfigItemsCollection> GetConfiguration()
        {
            ConfigItemsCollection configItemCollection = null;

            if (FileBasedConfigItemsSource != null)
                configItemCollection = await FileBasedConfigItemsSource.GetConfiguration().ConfigureAwait(false);
            return new MockConfigItemsCollection(GetConfigItemsOverrides, configItemCollection);
        }


        private Dictionary<string, ConfigItem> GetConfigItemsOverrides()
        {
            var items = new Dictionary<string, ConfigItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Data)
            {
                items.Add(item.Key, new ConfigItem(_configDecryptor)
                {
                    Key = item.Key,
                    Value = item.Value,
                    Overrides = new List<ConfigItemInfo>
                    {
                        new ConfigItemInfo {
                            FileName = @"c:\\dumy.config",
                            Priority = 1,
                            Value = item.Value}
                    }
                });
            }
            return items;
        }


        public void SetValue(string key, string value)
        {
            Data[key] = value;
        }

    }
}