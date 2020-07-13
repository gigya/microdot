using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Metrics.Utils
{
    public static class HostResolver
    {
        public static IPAddress Resolve(string host)
        {
            var address = Dns.GetHostAddresses(host)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .OrderBy(a => Guid.NewGuid())
                .FirstOrDefault();

            if (address == null)
            {
                throw new InvalidOperationException("Unable to resolve host name " + host);
            }

            return address;
        }
    }
}
