namespace Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint
{
    public class GCHandlingResult
    {
        public bool Successful { get; }
        public string Message { get; }
        public GCCollectionResult GcCollectionResult { get; }

        public GCHandlingResult(bool successful, string message = null, GCCollectionResult gcCollectionResult = null)
        {
            Successful = successful;
            Message = message;
            GcCollectionResult = gcCollectionResult;
        }
    }
}