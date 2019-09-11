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
using System.Collections.ObjectModel;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Hosting.Logging
{
    public class OrleansLogEnrichment
    {
        public ReadOnlyDictionary<int, string> HeuristicEventIdToName { get; private set; }

        public OrleansLogEnrichment()
        {
            var heuristicEventIdToName = new Dictionary<int, string>();
            Type type = typeof(LoggingUtils).Assembly.GetType("Orleans.ErrorCode");
            var names = Enum.GetValues(type);
            for (int i = 0; i < names.Length; i++)
            {
                var value = names.GetValue(i);
                heuristicEventIdToName[(int)value] = value.ToString();
            }
            HeuristicEventIdToName = new ReadOnlyDictionary<int, string>(heuristicEventIdToName);
        }
    }
}