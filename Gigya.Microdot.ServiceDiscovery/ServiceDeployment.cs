namespace Gigya.Microdot.ServiceDiscovery
{
    public class ServiceDeployment
    {
        public string DeploymentEnvironment { get; }
        public string ServiceName { get; }


        public ServiceDeployment(string serviceName, string deploymentEnvironment)
        {
            DeploymentEnvironment = deploymentEnvironment;
            ServiceName = serviceName;
        }


        public override string ToString()
        {
            return $"{ServiceName}-{DeploymentEnvironment}";
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            ServiceDeployment other = obj as ServiceDeployment;

            if (other == null)
                return false;

            return DeploymentEnvironment == other.DeploymentEnvironment && ServiceName == other.ServiceName;
        }


        public override int GetHashCode()
        {
            unchecked
            {
                return ((DeploymentEnvironment?.GetHashCode() ?? 0) * 397) ^ (ServiceName?.GetHashCode() ?? 0);
            }
        }
    }
}