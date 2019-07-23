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

        public string Name
        {
            get
            {
                if (Service.ServiceIpAddress != Node.NodeIpAddress && !string.IsNullOrEmpty(Service.ServiceIpAddress))
                    return Service.ServiceIpAddress;
                return Node.NodeName;
            }
        }

    }

    public class NodeEntry
    {
        [JsonProperty(PropertyName = "Node")]
        internal string NodeName { get; set; }

        [JsonProperty(PropertyName = "Address")]
        internal string NodeIpAddress { get; set; }

        //public string Name
        //{
        //    get
        //    {
        //        if (ServiceIpAddress != NodeIpAddress && !string.IsNullOrEmpty(ServiceIpAddress))
        //            return ServiceIpAddress;

        //        return NodeName;
        //    }
        //}

    }

    public class AgentService
    {
        private string _serviceIpAddress = string.Empty;
        public string[] Tags { get; set; }

        public int Port { get; set; }

        [JsonProperty(PropertyName = "Address")]
        internal string ServiceIpAddress
        {
            get => _serviceIpAddress;
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _serviceIpAddress = value;
            }
        }

    }


    public class KeyValueResponse
    {
        public string Value { get; set; }

        public T DecodeValue<T>() where T : class
        {
            var serialized = Encoding.UTF8.GetString(Convert.FromBase64String(Value));
            return JsonConvert.DeserializeObject<T>(serialized);
        }
    }

    public class ServiceKeyValue
    {
        [JsonProperty("version")]
        public string Version { get; set; }
    }
}
