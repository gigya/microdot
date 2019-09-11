using System.ComponentModel.DataAnnotations;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Orleans.Hosting
{
    [ConfigurationRoot("Microdot.GrainLogging", RootStrategy.ReplaceClassNameWithPath)]
    public class GrainLoggingConfig : IConfigObject
    {
        public bool LogServiceGrains { get; set; } = true;
        public bool LogMicrodotGrains { get; set; }
        public bool LogOrleansGrains { get; set; }

        /// <summary>
        /// This will what present are written 
        /// </summary>
        [Range(0.000000001,1)]
        public decimal LogRatio { get; set; } = 0.01m;
    }
}