using System;
using Newtonsoft.Json;

namespace Gigya.Microdot.Interfaces.HttpService
{
    [Serializable]
    public class HostOverride
    {
        [JsonProperty]
        public string ServiceName { get; set; }

        [JsonProperty]
        public string Host { get; set; }

        [JsonProperty]
        public int? Port { get; set; }
    }
}