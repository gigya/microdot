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
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.SharedLogic.HttpService.Schema
{
    public class AttributeSchema
    {
        [JsonIgnore]
        public Attribute Attribute { get; set; }

        public string TypeName { get; set; }

        public JObject Data { get; set; }

        public AttributeSchema() { }

        public AttributeSchema(Attribute attribute)
        {
            Attribute = attribute;
            TypeName = attribute.GetType().AssemblyQualifiedName;
            Data = JObject.FromObject(attribute);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            try
            {
                Type t = Type.GetType(TypeName);

                if (t != null)
                    Attribute = (Attribute)Data.ToObject(t);
            }
            catch { }
        }

        internal static bool FilterAttributes(Attribute a)
        {
            return a.GetType().Namespace?.StartsWith("System.Diagnostics") == false && a.GetType().Namespace?.StartsWith("System.Security") == false;
        }
    }

}
