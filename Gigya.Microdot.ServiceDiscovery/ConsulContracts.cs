using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gigya.Microdot.ServiceDiscovery.Rewrite;
using Newtonsoft.Json;

namespace Gigya.Microdot.ServiceDiscovery
{
    public class ConsulQueryExecuteResponse
    {
        public ServiceEntry[] Nodes { get; set; }
    }

    public class ServiceEntry
    {
        public NodeEntry Node { get; set; }

        public AgentService Service { get; set; }

        public Node ToNode()
        {
            const string versionPrefix = "version:";
            string versionTag = Service?.Tags?.FirstOrDefault(t => t.StartsWith(versionPrefix));
            string version = versionTag?.Substring(versionPrefix.Length);

            return new Node(Node.Name, Service?.Port, version);
        }
    }

    public class NodeEntry
    {
        [JsonProperty(PropertyName = "Node")]
        public string Name { get; set; }
    }

    public class AgentService
    {
        public string[] Tags { get; set; }

        public int Port { get; set; }

    }


    public class KeyValueResponse
    {
        public string Value { get; set; }

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
