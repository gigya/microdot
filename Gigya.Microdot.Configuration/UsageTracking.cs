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
using System.Collections.Concurrent;
using System.Linq;
using Gigya.Microdot.Interfaces.Logging;

namespace Gigya.Microdot.Configuration
{
    public class UsageTracking
    {
        private readonly ILog _log;
        private readonly ConcurrentDictionary<string, Type> _usageTracing = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<string, object> _objectTracking = new ConcurrentDictionary<string, object>();

        public UsageTracking(ILog log)
        {
            _log = log;
        }

        public Type Get(string configKey)
        {
            if (_usageTracing.TryGetValue(configKey, out Type result))
                return result;

            object configObject = null;
            object currentMember = null;
            try
            {
                var kvp = _objectTracking.FirstOrDefault(p => configKey.StartsWith(p.Key));
                var prefix = kvp.Key;
                configObject = kvp.Value;

                if (configObject == null)
                    return null;

                var pathParts = configKey.Substring(prefix.Length + 1).Split('.');

                currentMember = configObject;

                foreach (var pathPart in pathParts.Take(pathParts.Length))
                    currentMember = currentMember?.GetType().GetProperty(pathPart)?.GetValue(currentMember);
                
                return currentMember?.GetType();
            }
            catch (Exception e)
            {
                var currentMemberType = currentMember?.GetType();
                _log.Error("An attempt to track the usage of a configuration object threw an exception. See tags for details.", exception: e, unencryptedTags: new
                {
                    configKey,
                    configObjectType = configObject?.GetType(),
                    currentObjectType = currentMemberType?.DeclaringType?.Name,
                    currentMember = currentMemberType?.Name
                });
                return null;
            }
        }


        public void Add(string configKey, Type usedAs)
        {
            _usageTracing.AddOrUpdate(configKey, x => usedAs, (k, v) => usedAs);
        }


        public void AddConfigObject(object config, string path)
        {
            _objectTracking[path] = config;
        }
    }
}