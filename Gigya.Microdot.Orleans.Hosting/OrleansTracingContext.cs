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


using System.Collections.Generic;
using Gigya.Microdot.SharedLogic.Events;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Hosting
{

    public class OrleansTracingContext : TracingContextBase
    {
        private const string MICRODOT_KEY = "MicordotTracingData";

        public override IDictionary<string, object> Export()
        {
            return (IDictionary<string, object>)RequestContext.Get(MICRODOT_KEY);
        }

        protected override void Add(string key, object value)
        {
            var dictionary = (IDictionary<string, object>)RequestContext.Get(MICRODOT_KEY);
            IDictionary<string, object> cloneDictionary = null;

            if (dictionary == null)
                cloneDictionary = new Dictionary<string, object>();
            else
                cloneDictionary = new Dictionary<string, object>(dictionary);

            cloneDictionary[key] = value;
            RequestContext.Set(MICRODOT_KEY, cloneDictionary);
        }

        protected override T TryGetValue<T>(string key)
        {
            var dictionary = (IDictionary<string, object>)RequestContext.Get(MICRODOT_KEY);
            object result = null;

            dictionary?.TryGetValue(key, out result);

            return result as T;
        }

    }
}

