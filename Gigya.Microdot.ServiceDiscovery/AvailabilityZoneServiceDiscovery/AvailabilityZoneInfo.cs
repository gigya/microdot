using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    public class AvailabilityZoneInfo
    {
        public enum StatusCodes
        {
            InitializingConnectionToConsul = 0, // default before polling thread first (successful or failed) read
            Ok,
            FailedConnectToConsul,
            ConsulInternalError,
            MissingOrInvalidKeyValue,
            FailedGetHealthyNodes,
            FailedOrInvalidKeyFromConsul,
            CriticalError
        }

        public StatusCodes StatusCode { get; internal set; }
        public string ServiceName { get; internal set; }
        public string ServiceZone { get; internal set; }
        public DeploymentIdentifier DeploymentIdentifier { get; internal set; }
        public Node[] Nodes { get; internal set; }
        public EnvironmentException Exception { get; internal set; }
    }
}