using System.Collections.Generic;

namespace Gigya.Microdot.Configuration
{

    public interface IConfigurationLocationsParser
    {
        string ConfigRoot { get; }

        string LoadPathsFilePath { get; }

        IList<ConfigFileDeclaration> ConfigFileDeclarations { get; }

        string TryGetFileFromConfigLocations(string fileName);
    }
}