using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.ServiceProxy.Caching.RevokeNotifier
{
    public class RevokeNotifierConfig: IConfigObject
    {
        public int CleanupIntervalInSec { get; set; } = 10*60;
    }
}
