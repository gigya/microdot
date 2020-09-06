using Newtonsoft.Json;

namespace Gigya.Common.Application
{
    internal class DbKeyValue
    {
        [JsonProperty("serviceZone")]
        public string ServiceZone { get; set; }

        [JsonProperty("consulZone")]
        public string ConsulZone { get; set; }
    }
}