using System;
using System.Runtime.Serialization;

using Gigya.Microdot.ServiceContract.Exceptions;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    [Serializable]
    public class ConfigurationException : EnvironmentException
    {
        public ConfigurationException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null)
            : base(message, innerException, encrypted, unencrypted)
        {
        }
        
        public ConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}