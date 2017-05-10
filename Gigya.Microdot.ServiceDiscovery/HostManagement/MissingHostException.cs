using System;
using System.Runtime.Serialization;

using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.HostManagement
{
    [Serializable]
    public class MissingHostException:EnvironmentException
    {
        public MissingHostException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null)
            : base(message, innerException, encrypted, unencrypted) { }


        public MissingHostException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
