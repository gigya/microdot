using System;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.SharedLogic.Configurations
{

    [Serializable]
    [ConfigurationRoot("Microdot.LoadShedding", RootStrategy.ReplaceClassNameWithPath)]
    public class LoadShedding : IConfigObject
    {
        public bool ApplyToServiceGrains { get; set; } = true;
        public bool ApplyToMicrodotGrains { get; set; } = false;
        //We can add specific grain in dic to the filter internal orleans or service, not recommend to block all OrleansGrains
        

        public enum Toggle
        {
            Disabled,
            LogOnly,
            Drop,
        }

        public Toggle DropRequestsByDeathTime { get; set; } = Toggle.Disabled;
        public TimeSpan RequestTimeToLive { get; set; } = TimeSpan.FromSeconds(90);
        public TimeSpan TimeToDropBeforeDeathTime { get; set; } = TimeSpan.FromSeconds(5);

        public Toggle DropMicrodotRequestsBySpanTime { get; set; } = Toggle.Disabled;
        public TimeSpan DropMicrodotRequestsOlderThanSpanTimeBy { get; set; } = TimeSpan.FromSeconds(5);

        public Toggle DropOrleansRequestsBySpanTime { get; set; } = Toggle.Disabled;
        public TimeSpan DropOrleansRequestsOlderThanSpanTimeBy { get; set; } = TimeSpan.FromSeconds(5);
    }

}
