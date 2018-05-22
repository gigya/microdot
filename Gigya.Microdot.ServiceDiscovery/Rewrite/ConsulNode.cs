using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    internal class ConsulNode: Node
    {
        public ConsulNode(string hostName, int? port = null, string version=null) : base(hostName, port)
        {
            Version = version;
        }

        /// <summary>
        /// Version of this node (relevant only for Consul nodes)
        /// </summary>
        public string Version { get; }
    }
}
