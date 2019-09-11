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

    public class TracingContextNoneOrleans 
    {

        private readonly AsyncLocal<Dictionary<string, object>> CallContextData = new AsyncLocal<Dictionary<string, object>>();

        public  object Get(string key)
        {
            var values = CallContextData.Value;

            if (values != null && values.TryGetValue(key, out var result))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// Sets a value into the RequestContext key-value bag.
        /// </summary>
        /// <param name="key">The key for the value to be updated / added.</param>
        /// <param name="value">The value to be stored into RequestContext.</param>
        public  void Set(string key, object value)
        {
            var values = CallContextData.Value;

            if (values == null)
            {
                values = new Dictionary<string, object>(1);
            }
            else
            {
                /*
                   _____ ____  _______     __   ____  _   _  __          _______  _____ _______ ______ 
                  / ____/ __ \|  __ \ \   / /  / __ \| \ | | \ \        / /  __ \|_   _|__   __|  ____|
                 | |   | |  | | |__) \ \_/ /  | |  | |  \| |  \ \  /\  / /| |__) | | |    | |  | |__   
                 | |   | |  | |  ___/ \   /   | |  | | . ` |   \ \/  \/ / |  _  /  | |    | |  |  __|  
                 | |___| |__| | |      | |    | |__| | |\  |    \  /\  /  | | \ \ _| |_   | |  | |____ 
                  \_____\____/|_|      |_|     \____/|_| \_|     \/  \/   |_|  \_\_____|  |_|  |______|
                                                                                                       
                 */

                // Have to copy the actual Dictionary value, mutate it and set it back.
                // This is since AsyncLocal copies link to dictionary, not create a new one.
                // So we need to make sure that modifying the value, we doesn't affect other threads.

                var hadPreviousValue = values.ContainsKey(key);
                var newValues = new Dictionary<string, object>(values.Count + (hadPreviousValue ? 0 : 1));
                foreach (var pair in values)
                {
                    newValues.Add(pair.Key, pair.Value);
                }

                values = newValues;
            }

            values[key] = value;
            CallContextData.Value = values;
        }
    }
}