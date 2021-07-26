using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Gigya.Microdot.Interfaces.Configuration;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.Configurations.Serialization
{
    [ConfigurationRoot("Microdot.SerializationSecurity", RootStrategy.ReplaceClassNameWithPath)]
    public class MicrodotSerializationSecurityConfig : IConfigObject
    {
        public List<string> DeserializationForbiddenTypes;
        public List<AssemblyNameToRegexReplacement> AssemblyNamesRegexReplacements;
      
        [OnDeserialized]
        private void OnDeserialized(System.Runtime.Serialization.StreamingContext context)
        {
            if (DeserializationForbiddenTypes == null)
            {
                DeserializationForbiddenTypes = new List<string>(){
                    "System.Windows.Data.ObjectDataProvider",
                    "System.Diagnostics.Process",
                    "System.Configuration.Install.AssemblyInstaller",
                    "System.Activities.PresentationWorkflowDesigner",
                    "System.Windows.ResourceDictionary",
                    "System.Windows.Forms.BindingSource",
                    "Microsoft.Exchange.Management.SystemManager.WinForms.ExchangeSettingsProvider"
                };
            }

            if (AssemblyNamesRegexReplacements == null)
                AssemblyNamesRegexReplacements = new List<AssemblyNameToRegexReplacement>()
                {
                    new AssemblyNameToRegexReplacement("System.Private.CoreLib", "mscorlib")
                };
        }

        public class AssemblyNameToRegexReplacement
        {
            public Regex AssemblyRegularExpression;
            public string AssemblyToReplace;
            public string AssemblyReplacement;

            public AssemblyNameToRegexReplacement()
            {
            }

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