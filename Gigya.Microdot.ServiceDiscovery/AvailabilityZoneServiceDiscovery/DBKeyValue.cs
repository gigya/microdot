using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    internal class DbKeyValue
    {
        [JsonProperty("serviceZone")]
        public string ServiceZone { get; set; }

        [JsonProperty("consulZone")]
        public string ConsulZone { get; set; }
    }
}