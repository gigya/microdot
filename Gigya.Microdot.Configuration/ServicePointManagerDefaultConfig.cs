using System;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Configuration
{
    [Serializable]
    [ConfigurationRoot("Networking.ServicePointManager", RootStrategy.ReplaceClassNameWithPath)]
    public class ServicePointManagerDefaultConfig : IConfigObject
    {
        public int DefaultConnectionLimit { get; set; } = 500;
        public bool UseNagleAlgorithm { get; set; } = false;
        public bool Expect100Continue { get; set; } = false;
    }
}
