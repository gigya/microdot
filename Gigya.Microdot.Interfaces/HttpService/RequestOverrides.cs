using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Gigya.Microdot.Interfaces.HttpService
{
    [Serializable]
    public class RequestOverrides
    {
        [JsonProperty]
        public List<HostOverride> Hosts { get; set; }
    }

    [Serializable]
    public class HostOverride
    {
        [JsonProperty]
        public string ServiceName { get; set; }

        [JsonProperty]
        public string Host { get; set; }
    }
}