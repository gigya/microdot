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
using System.Collections.Immutable;
using System.Linq;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    public abstract class ConfigCollection<T> : IImmutableDictionary<string, T>
    {
        private Lazy<IImmutableDictionary<string, T>> Source { get; }

        protected T DefaultItem { get; }


        public T this[string key]
        {
            get
            {
                T item;
                TryGetValue(key, out item);
                return item;
            }
        }


        public ConfigCollection(IDictionary<string, T> source, T defaultItem)
        {
            DefaultItem = defaultItem;
            Source = new Lazy<IImmutableDictionary<string, T>>(() => 
                        source.ToDictionary(kvp => kvp.Key, kvp => ApplyDefaults(kvp.Value))
                        .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));
        }


        protected abstract T ApplyDefaults(T item);


        public bool Contains(KeyValuePair<string, T> item)
        {
            if (Source.Value.ContainsKey(item.Key) == false)
                return true;

            return Source.Value.Contains(item);
        }


        public bool TryGetKey(string equalKey, out string actualKey)
        {
            actualKey = equalKey;
            return true;
        }


        public bool ContainsKey(string key) => true;


        public bool TryGetValue(string key, out T value)
        {
            if (Source.Value.TryGetValue(key, out value) == false)
                value = DefaultItem;

            return true;
        }

        #region IDictionary delegation

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => Source.Value.GetEnumerator();
        public IImmutableDictionary<string, T> Clear() => Source.Value.Clear();
        public IImmutableDictionary<string, T> Add(string key, T value) => Source.Value.Add(key, value);
        public IImmutableDictionary<string, T> AddRange(IEnumerable<KeyValuePair<string, T>> pairs) => Source.Value.AddRange(pairs);
        public IImmutableDictionary<string, T> SetItem(string key, T value) => Source.Value.SetItem(key, value);
        public IImmutableDictionary<string, T> SetItems(IEnumerable<KeyValuePair<string, T>> items) => Source.Value.SetItems(items);
        public IImmutableDictionary<string, T> RemoveRange(IEnumerable<string> keys) => Source.Value.RemoveRange(keys);
        public IImmutableDictionary<string, T> Remove(string key) => Source.Value.Remove(key);

        public IEnumerable<string> Keys => Source.Value.Keys;

        public IEnumerable<T> Values => Source.Value.Values;


        public int Count => Source.Value.Count;


        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Source.Value).GetEnumerator();
        }


        #endregion


    }
}