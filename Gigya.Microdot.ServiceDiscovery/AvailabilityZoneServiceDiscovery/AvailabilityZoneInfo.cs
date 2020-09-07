using System;
using System.Collections.Generic;
using System.Text;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.AvailabilityZoneServiceDiscovery
{
    public class AvailabilityZoneInfo
    {
        public enum StatusCodes
        {
            Ok = 0,
            FailedConnectToConsul,
            MissingOrInvalidKeyValue,
            FailedGetHealthyNodes,
            CriticalError,
            FailedOrInvalidKeyFromConsul
        }

        public StatusCodes StatusCode;
        public string ServiceName;
        public string ServiceZone;
        public DeploymentIdentifier DeploymentIdentifier;
        public Node[] Nodes;
        public EnvironmentException Exception;

    }
}
