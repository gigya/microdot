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

namespace Gigya.Microdot.Interfaces.Events
{    
    /// <summary>Indicates this field should be written, and what name to use for the field name.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]    
    public class EventFieldAttribute : Attribute
    {

        /// <summary>A default value to write in case the field or property is null (e.g. "" or 0).</summary>
        public object DefaultValue;

        public readonly string Name;
        public bool OnlyForAudit = false;
        public bool OmitFromAudit = false;

        /// <summary>Whether the field value should be encrypted.</summary>
        public bool Encrypt = false;

        /// <summary>Will append "_i", "_f", etc to the name of fields based on their types when enumerating dictionaries.
        /// Cannot be used along with <see cref="Encrypt"/>, since encrypted tags are by definition always strings.</summary>
        public bool AppendTypeSuffix;

        public bool TruncateIfLong = false;

        public EventFieldAttribute(string _name, object default_value = null)
        {
            Name = _name;
            DefaultValue = default_value;
        }
    }
}
