using System;

namespace Gigya.Microdot.Interfaces.Configuration
{
    public enum RootStrategy
    {
        AppendClassNameToPath,
        ReplaceClassNameWithPath
    }

    /// <summary>
    /// Should be placed on configuration objects if you want to control a path in configuration where object is created from,
    /// the path property is path, the second argument determines eather the path should be appended to class name or it should replace it entirely.  
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigurationRootAttribute:Attribute
    {
        /// <param name="path">Configuration path.</param>
        /// <param name="buildingStrategy">Determines the strategy for usage of path should it be appended to class name as prefix or should it replace the class name.</param>
        public ConfigurationRootAttribute(string path, RootStrategy buildingStrategy)
        {
            if(string.IsNullOrEmpty(path))
            {
                throw new ArgumentOutOfRangeException(nameof(path));
            }
            BuildingStrategy = buildingStrategy;
            Path = path;
        }
        public RootStrategy BuildingStrategy { get; }
        public string Path { get; }
    }
}