using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class Node : INode
    {
        public Node(string hostName, int? port = null, string version=null)
        {
            Hostname = hostName;
            Port = port;
            Version = version;
        }

        public string Hostname { get; }
        public int? Port { get; }
        
        /// <summary>
        /// Version of this node (relevant only for Consul nodes)
        /// </summary>
        public string Version { get; }

        public override string ToString()
        {
            return Port.HasValue ? $"{Hostname}:{Port}" : Hostname;
        }
    }
}
