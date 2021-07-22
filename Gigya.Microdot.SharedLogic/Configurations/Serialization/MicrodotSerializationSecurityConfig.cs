using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gigya.Microdot.Interfaces.Configuration;
using Newtonsoft.Json;

namespace Gigya.Microdot.SharedLogic.Configurations.Serialization
{
    [ConfigurationRoot("Microdot.SerializationSecurity", RootStrategy.ReplaceClassNameWithPath)]
    public class MicrodotSerializationSecurityConfig : IConfigObject
    {
        public Dictionary<string, bool> DeserializationForbiddenTypes { get; set; } = new Dictionary<string, bool>(){
            { "System.Windows.Data.ObjectDataProvider", true},
            {"System.Diagnostics.Process", true},
            {"System.Configuration.Install.AssemblyInstaller",true},
            {"System.Activities.PresentationWorkflowDesigner",true},
            {"System.Windows.ResourceDictionary", true },
            {"System.Windows.Forms.BindingSource", true},
            {"Microsoft.Exchange.Management.SystemManager.WinForms.ExchangeSettingsProvider", true}
        };
        public Dictionary<string, string> AssemblyNamesRegexReplacements{ get; set; }= new Dictionary<string, string>();

    }
}