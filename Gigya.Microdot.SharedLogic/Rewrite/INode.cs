namespace Gigya.Microdot.SharedLogic.Rewrite
{
    public interface INode
    {
        string Hostname { get; }
        int? Port { get; }
    }
}
