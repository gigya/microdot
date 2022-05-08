using Gigya.Microdot.Interfaces.Configuration;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    [ConfigurationRoot("ConfigurationResponseBuilder", RootStrategy.ReplaceClassNameWithPath)]
    public class ConfigurationResponseBuilderConfig : IConfigObject
    {
        [JsonProperty]
        private string EnvsToPublish { get; set; } = "DC, ZONE, REGION, ENV, CONSUL, OS, HOSTIPADDRESS";

        [JsonIgnore]
        public List<string> envs { get; private set; } 

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            envs = EnvsToPublish?
                .Split(new[] { '\n', '\r', '\t', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(_ => _.Trim())
                .Where(_ => !string.IsNullOrEmpty(_))
                .OrderBy(_ => _)
                .ToList()
                ?? new List<string>();
        }
    }
}
