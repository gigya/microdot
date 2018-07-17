using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Exceptions;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    internal static class Ex
    {

        public static ConfigurationException IncorrectHostFormatInConfig(string hosts, string serviceName)
        {
            return new ConfigurationException("A config-specified hostname name must contain at most one colon (:).",
                unencrypted: new Tags
                {
                    { "hosts", hosts },
                    { "configPath", $"Discovery.{serviceName}.Hosts" },
                });
        }
    }
}
