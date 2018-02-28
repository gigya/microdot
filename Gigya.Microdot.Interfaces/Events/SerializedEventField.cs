
namespace Gigya.Microdot.Interfaces.Events
{

    /// <summary>
    /// Represents a single serialized field marked as <see cref="EventFieldAttribute"/> from an <see cref="IEvent"/>
    /// object, or a sub-field of a collection. Beware of writing the <see cref="Value"/> if <see cref="ShouldEncrypt"/>
    /// is true.
    /// </summary>
    public class SerializedEventField
    {
        public string Name;
        public string Value;
        public bool   ShouldEncrypt;
    }
}
