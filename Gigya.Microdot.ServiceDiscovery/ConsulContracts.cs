using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class ConsulQueryExecuteResponse
    {
        public string Service { get; set; }

        public ServiceEntry[] Nodes { get; set; }

        public QueryDNSOptions DNS { get; set; }

        public string Datacenter { get; set; }

        public int Failovers { get; set; }
    }

    public class ServiceEntry
    {
        public Node Node { get; set; }

        public AgentService Service { get; set; }

        public HealthCheck[] Checks { get; set; }
    }

    public class Node
    {
        [JsonProperty(PropertyName = "Node")]
        public string Name { get; set; }

        public string Address { get; set; }

        public ulong ModifyIndex { get; set; }

        public Dictionary<string, string> TaggedAddresses { get; set; }
    }

    public class AgentService
    {
        public string ID { get; set; }

        public string Service { get; set; }

        public string[] Tags { get; set; }

        public int Port { get; set; }

        public string Address { get; set; }

        public bool EnableTagOverride { get; set; }
    }

    public class HealthCheck
    {
        public string Node { get; set; }

        public string CheckID { get; set; }

        public string Name { get; set; }

        public string Status { get; set; }

        public string Notes { get; set; }

        public string Output { get; set; }

        public string ServiceID { get; set; }

        public string ServiceName { get; set; }
    }

    public class QueryDNSOptions
    {
        public string TTL { get; set; }
    }

    public class KeyValueResponse
    {
        public int LockIndex { get; set; }
        public string Key { get; set; }
        public int Flags { get; set; }
        public string Value { get; set; }
        public ulong CreateIndex { get; set; }
        public ulong ModifyIndex { get; set; }

        public ServiceKeyValue TryDecodeValue()
        {
            if (Value == null)
                return null;

            try
            {
                var serialized = Encoding.UTF8.GetString(Convert.FromBase64String(Value));
                return JsonConvert.DeserializeObject<ServiceKeyValue>(serialized);
            }
            catch
            {
                return null;
            }
        }
    }

    public class ServiceKeyValue
    {
        [JsonProperty("basePort")]
        public int BasePort { get; set; }

        [JsonProperty("dc")]
        public string DataCenter { get; set; }

        [JsonProperty("env")]
        public string Environment { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }
}
