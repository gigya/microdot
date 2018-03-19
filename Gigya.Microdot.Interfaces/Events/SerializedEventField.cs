
using System;

namespace Gigya.Microdot.Interfaces.Events
{

    /// <summary>
    /// Represents a single serialized field marked as <see cref="EventFieldAttribute"/> from an <see cref="IEvent"/>
    /// object, or a sub-field of a collection. Beware of writing the <see cref="Value"/> if <see cref="Attribute"/>
    /// denotes it should be encrypted.
    /// </summary>
    public class SerializedEventField
    {
        public string Name;
        public string Value;
        public EventFieldAttribute Attribute;

        [Obsolete]
        public bool ShouldEncrypt; // TODO: Remove
    }
}
