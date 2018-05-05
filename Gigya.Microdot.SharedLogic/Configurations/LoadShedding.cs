using System;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.SharedLogic.Configurations
{

    [Serializable]
    [ConfigurationRoot("Microdot.LoadShedding", RootStrategy.ReplaceClassNameWithPath)]
    public class LoadShedding : IConfigObject
    {
        public enum Toggle
        {
            No,
            LogOnly,
            Drop,
        }

        public Toggle   DropRequestsByDeathTime   { get; set; } = Toggle.No;
        public TimeSpan RequestTimeToLive         { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan TimeToDropBeforeDeathTime { get; set; } = TimeSpan.FromSeconds(5);

        public Toggle   DropMicrodotRequestsBySpanTime          { get; set; } = Toggle.No;
        public TimeSpan DropMicrodotRequestsOlderThanSpanTimeBy { get; set; } = TimeSpan.FromSeconds(5);

        public Toggle   DropOrleansRequestsBySpanTime           { get; set; } = Toggle.No;
        public TimeSpan DropOrleansRequestsOlderThanSpanTimeBy  { get; set; } = TimeSpan.FromSeconds(5);
    }

}
