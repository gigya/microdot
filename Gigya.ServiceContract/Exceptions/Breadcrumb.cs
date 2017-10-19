using System;

namespace Gigya.ServiceContract.Exceptions
{
    [Serializable]
    public class Breadcrumb
    {
        public string ServiceName { get; set; }
        public string ServiceVersion { get; set; }
        public string HostName { get; set; }
        public string DataCenter { get; set; }
        public string DeploymentEnvironment { get; set; }

        public override string ToString()
        {
            return $"{ServiceName} v{ServiceVersion} on {HostName} in {DataCenter}-{DeploymentEnvironment}";
        }
    }
}
