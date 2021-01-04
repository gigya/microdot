using System;
using Gigya.Microdot.Interfaces.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    [Serializable]
    [ConfigurationRoot("Microdot.ExceptionSerialization", RootStrategy.ReplaceClassNameWithPath)]
    public class ExceptionSerializationConfig:IConfigObject
    {
        public bool UseNetCoreToFrameworkTypeTranslation { get;  }
        public bool UseNetCoreToFrameworkNameTranslation { get;  }

        private ExceptionSerializationConfig()
        {
        }

        [JsonConstructor]
        public ExceptionSerializationConfig(bool useNetCoreToFrameworkTypeTranslation, bool useNetCoreToFrameworkNameTranslation)
        {
            UseNetCoreToFrameworkTypeTranslation = useNetCoreToFrameworkTypeTranslation;
            UseNetCoreToFrameworkNameTranslation = useNetCoreToFrameworkNameTranslation;
        }
    }
}