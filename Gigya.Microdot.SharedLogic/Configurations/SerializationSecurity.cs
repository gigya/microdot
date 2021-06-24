using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.SharedLogic.Configurations
{
    [ConfigurationRoot("Microdot.SerializationSecurity", RootStrategy.ReplaceClassNameWithPath)]
    public class MicrodotSerializationSecurityConfig : IConfigObject
    {
        public string DeserializationForbiddenTypes = "System.Windows.Data.ObjectDataProvider,System.Diagnostics.Process,System.Configuration.Install.AssemblyInstaller,System.Activities.PresentationWorkflowDesigner,System.Windows.ResourceDictionary,System.Windows.Forms.BindingSource,Microsoft.Exchange.Management.SystemManager.WinForms.ExchangeSettingsProvider";
    }

}
