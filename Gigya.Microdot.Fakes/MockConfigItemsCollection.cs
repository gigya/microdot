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

using Gigya.Microdot.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gigya.Microdot.Fakes
{
    internal class MockConfigItemsCollection : ConfigItemsCollection
    {
        private ConfigItemsCollection ConfigItemCollection { get; }

        private readonly Func<Dictionary<string, ConfigItem>> configItemsFunc;

        public MockConfigItemsCollection(Func<Dictionary<string, ConfigItem>> configItems, ConfigItemsCollection configItemCollection = null)
            : base(Enumerable.Empty<ConfigItem>())
        {
            ConfigItemCollection = configItemCollection;

            configItemsFunc = configItems;
        }


        public override IEnumerable<ConfigItem> Items
        {
            get
            {
                var result = new List<ConfigItem>();
                var data = configItemsFunc();
                result.AddRange(data.Values);
                if (ConfigItemCollection != null)
                {
                    result.AddRange(ConfigItemCollection.Items.Where(x=> !data.ContainsKey(x.Key)));
                }
                return result;
            }
        }


        public override ConfigItem TryGetConfigItem(string key)
        {
            key = key.ToLowerInvariant();

            var data = configItemsFunc();
            if (data.ContainsKey(key))
            {
                return data[key];
            }

            return ConfigItemCollection?.TryGetConfigItem(key);
        }
    }
}