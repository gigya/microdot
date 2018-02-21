using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceProxy.Caching
{
    [ConfigurationRoot("Revoke", RootStrategy.ReplaceClassNameWithPath)]
    public class RevokeConfig: IConfigObject
    {
        public bool LogRequests { get; set; } = false;
    }
}
