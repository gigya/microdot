using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;

using Gigya.Microdot.ServiceContract.Exceptions;
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
        /// <summary>
        /// Public for Legacy do not change to internal
        /// </summary>
        public static Func<string, string> ConfigDecryptor = null;
        public static Func<string, bool>   IsValidEncryptedStringFormat = null;

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
            set { _overrides = value; }
        }

        public string Value
        {
            get
            {
                if(DecryptedValue == null && RawValue != null 
                   && IsValidEncryptedStringFormat != null 
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
                                                                       if (IsValidEncryptedStringFormat(inner))
                                                                       {
                                                                           try
                                                                           {
                                                                               return ConfigDecryptor(inner);
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