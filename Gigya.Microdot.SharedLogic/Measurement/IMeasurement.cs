namespace Gigya.Microdot.SharedLogic.Measurement
{
    /// <summary>A time measurement. The number of calls made and their total time taken.</summary>
    public interface IMeasurement {
        double? ElapsedMS { get; }
        long?   Calls     { get; }
    }
}