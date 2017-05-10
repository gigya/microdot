using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.Orleans.Hosting
{
    public class OrleansCodeConfig : BaseCommonConfig
    {
        public OrleansCodeConfig(ServiceArguments serviceArguments = null)
            : base(serviceArguments)
        { }

        /// <summary>
        /// Whether to initialize the persistent reminders backend. Setting this to false and trying to register a
        /// reminder throws an exception.
        /// </summary>
        public bool UseReminders { get; set; }

        public string StorageProviderTypeFullName { get; set; }

        public string StorageProviderName { get; set; }
    }
}