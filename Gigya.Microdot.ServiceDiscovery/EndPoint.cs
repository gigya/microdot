
namespace Gigya.Microdot.ServiceDiscovery
{
    public class EndPoint
    {
        public string HostName { get; set; }
        public int? Port { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as EndPoint;
            if (other==null)
                return false;

            return other.HostName==this.HostName && other.Port==this.Port;
        }


        public override int GetHashCode()
        {
            unchecked
            {
                return ((HostName?.GetHashCode() ?? 0)*397) ^ Port.GetHashCode();
            }
        }


        public override string ToString()
        {
            return Port.HasValue ? $"{HostName}:{Port}" : HostName;
        }
    }
}
