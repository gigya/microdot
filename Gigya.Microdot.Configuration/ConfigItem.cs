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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic.Exceptions;

namespace Gigya.Microdot.Configuration
{
    [DebuggerDisplay("{" + nameof(Key) + "}")]
    public class ConfigItem : IConfigItem
    {
        internal string RawValue { get; set; }
        private string DecryptedValue { get; set; }
        public string Key { get; set; }
        public uint Priority { get; set; }

        public  ConfigItem(ConfigDecryptor configDecryptor)
        {
            ConfigDecryptor = configDecryptor;
        }

        /// <summary>
        /// Public for Legacy do not change to internal
        /// </summary>
        private  ConfigDecryptor ConfigDecryptor  { get; set; }

        private List<ConfigItemInfo> _overrides;

        /// <summary>
        /// Gets or sets a list of all the values this config item had, including the one that is the current value.
        /// </summary>
        public List<ConfigItemInfo> Overrides
        {
            get
            {
                if (_overrides == null)
                    _overrides = new List<ConfigItemInfo>();
                return _overrides;
            }
            set => _overrides = value;
        }

        public string Value
        {
            get
            {
                if(DecryptedValue == null && RawValue != null 
                   && ConfigDecryptor.IsValidEncryptedStringFormat != null 
                   && ConfigDecryptor != null) {
                    DecryptedValue = DecryptRawValue(RawValue);
                }
                return DecryptedValue ?? RawValue;
            }
            set
            {
                RawValue = value;
                DecryptedValue = null;
            }
        }

        public XmlNode Node { get; set; }


        static readonly Regex MATCH_ENCRYPTED_CONFIG_STRING = new Regex(@"\$enc\((?<aaa>.*?)\)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled);


        private string DecryptRawValue(string rawValue)
        {
            return MATCH_ENCRYPTED_CONFIG_STRING.Replace(rawValue, m =>
               {
                   var inner = m.Groups[1].Value;
                   if (ConfigDecryptor. IsValidEncryptedStringFormat(inner))
                   {
                       try
                       {
                           return ConfigDecryptor.ConfigDecryptorFunc(inner);
                       }
                       catch(Exception e)
                       {
                           throw new ConfigurationException($"Cannot decrypt configuration Key: {Key}", e);
                       }                    
                   }
                   else
                   {
                       throw new ConfigurationException("String is decorated with encryption prefix but it not in valid format and cannot be decrypted", unencrypted: new Tags {{"key", Key}});
                   }
               });
        }

    }
}