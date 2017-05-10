using System;

using Newtonsoft.Json;

namespace Gigya.Microdot.Configuration
{
    public class ConfigFileDeclaration: IComparable<ConfigFileDeclaration>
    {
        [JsonProperty(Required = Required.Always)]
        public string Pattern { get; set; }

        [JsonProperty(Required = Required.Always)]
        public uint Priority { get; set; }

        public int CompareTo(ConfigFileDeclaration other)
        {
            return Priority == other.Priority 
                ? string.Compare(Pattern, other.Pattern, StringComparison.Ordinal) 
                : (int) other.Priority - (int) Priority;
        }
    }
}