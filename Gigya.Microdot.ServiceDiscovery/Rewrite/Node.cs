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

        public override bool Equals(object obj)
        {
            var other = obj as INode;
            if (other == null)
                return false;

            return other.Hostname == Hostname && other.Port == Port;
        }


        public override int GetHashCode()
        {
            unchecked
            {
                return ((Hostname?.GetHashCode() ?? 0) * 397) ^ (Port?.GetHashCode()??1);
            }
        }
    }
}
