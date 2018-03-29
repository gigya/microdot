namespace Gigya.Microdot.Hosting.Validators
{
    /// <summary>
    /// When service is started, validates that the service is Ok to run, and throws an exception if not
    /// </summary>
    public interface IValidator
    {
        void Validate();
    }
}
