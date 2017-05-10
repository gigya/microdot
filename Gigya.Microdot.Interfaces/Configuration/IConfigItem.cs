using System.Collections.Generic;

namespace Gigya.Microdot.Interfaces.Configuration
{
    public interface IConfigItem
    {
        string Key { get; set; }

        string Value { get; set; }

        List<ConfigItemInfo> Overrides { get; }
    }
}