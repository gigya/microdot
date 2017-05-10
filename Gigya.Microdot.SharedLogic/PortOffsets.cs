namespace Gigya.Microdot.SharedLogic
{
    public enum PortOffsets
    {
        Http = 0,
        SiloGateway = 1, // TODO: Once everyone is using the ServiceProxy, don't open the silo gateway port
        SiloNetworking = 2,
        Metrics = 3
    }
}
