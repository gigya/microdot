using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gigya.Microdot.Interfaces.Configuration;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.Configurations.Serialization
{
    [ConfigurationRoot("Microdot.SerializationSecurity", RootStrategy.ReplaceClassNameWithPath)]
    public class MicrodotSerializationSecurityConfig : IConfigObject
    {
        public List<string> DeserializationForbiddenTypes { get; }
        public List<AssemblyNameToRegexReplacement> AssemblyNamesRegexReplacements { get; }
     
        [JsonConstructor]
        public MicrodotSerializationSecurityConfig(List<string> deserializationForbiddenTypes, 
            List<AssemblyNameToRegexReplacement> assemblyNamesRegexReplacements)
        {
            if (deserializationForbiddenTypes == null)
            {
                deserializationForbiddenTypes = new List<string>(new[]
                {
                    "System.Windows.Data.ObjectDataProvider",
                    "System.Diagnostics.Process",
                    "System.Configuration.Install.AssemblyInstaller",
                    "System.Activities.PresentationWorkflowDesigner",
                    "System.Windows.ResourceDictionary",
                    "System.Windows.Forms.BindingSource",
                    "Microsoft.Exchange.Management.SystemManager.WinForms.ExchangeSettingsProvider"
                });
            }
            
            DeserializationForbiddenTypes = deserializationForbiddenTypes;
            AssemblyNamesRegexReplacements = assemblyNamesRegexReplacements??new List<AssemblyNameToRegexReplacement>();
        }

        public class AssemblyNameToRegexReplacement
        {
            //System\.Private\.CoreLib
            //@"System\.Private\.CoreLib(, Version=[\d\.]+)?(, Culture=[\w-]+)?(, PublicKeyToken=[\w\d]+)?"
            public Regex AssemblyRegularExpression { get; }
            public string AssemblyToReplace { get; }
            public string AssemblyReplacement { get; }
            
            [JsonConstructor]
            public AssemblyNameToRegexReplacement(string assemblyToReplace, string assemblyReplacement)
            {
                AssemblyRegularExpression = new Regex($@"{assemblyToReplace.Replace(".", @"\.")}(, Version=[\d\.]+)?(, Culture=[\w-]+)?(, PublicKeyToken=[\w\d]+)?", RegexOptions.Compiled);
                AssemblyReplacement = assemblyReplacement;
                AssemblyToReplace = assemblyToReplace;
            }
        }
    }
}