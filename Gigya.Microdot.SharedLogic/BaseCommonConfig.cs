namespace Gigya.Microdot.SharedLogic
{
    /// <summary>Describes what kinds of infrastructures should be initialized, and how to initialize them.</summary>
    public class BaseCommonConfig
    {
        public ServiceArguments ServiceArguments { get; }

        /// <summary>
        /// If specified, sets the working diretory to the specified path, otherwise sets the current working directory
        /// to the location of the executable.
        /// </summary>
        public string ApplicationDirectoryOverride { get; set; }


        /// <summary>
        /// The assembly file names specified will not be automatically loaded from the working directory.
        /// </summary>
        public string[] AssemblyScanningBlacklist { get; set; }


        public BaseCommonConfig(ServiceArguments serviceArguments = null)
        {
            ServiceArguments = serviceArguments ?? new ServiceArguments();
        }
    }
}