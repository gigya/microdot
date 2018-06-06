namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    /// <summary>
    /// Defines the strategy to use on LoadBalancer, for routing the traffic between the different nodes
    /// </summary>
    public enum TrafficRoutingStrategy
    {
        /// <summary>
        /// The traffic will be routed to a different node for each call, so that the traffic will spread equally between all nodes
        /// </summary>
        RoundRobin,

        /// <summary>
        /// The traffic will be routed randomly between nodes. The random algorithm will be based on the call's RequestID, 
        /// so that all calls with same callID will get to the same node. 
        /// This way we can prevent duplicate activation of Grains, because all same requests will always get to the same nodes.
        /// </summary>
        RandomByRequestID
    }
}
