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
using System.Threading;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class ServiceTracingContext : TracingContextBase
    {
        private AsyncLocal<Dictionary<string, object>> Context { get; } = new AsyncLocal<Dictionary<string, object>>();

        public override IDictionary<string, object> Export()
        {
            return Context.Value;
        }

        protected override void Add(string key, object value)
        {
            Dictionary<string, object> cloneDictionary = null;

            if (Context.Value == null)
                cloneDictionary= new Dictionary<string, object>();
            else
                cloneDictionary = new Dictionary<string, object>(Context.Value);

            cloneDictionary[key] = value;

            Context.Value = cloneDictionary;
        }

        protected override T TryGetValue<T>(string key)
        {
            if (Context.Value == null)
            {
                return null;
            }

            Context.Value.TryGetValue(key, out var result);
            return result as T;
        }
    }
}