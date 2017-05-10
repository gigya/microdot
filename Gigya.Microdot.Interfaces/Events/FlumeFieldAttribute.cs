using System;

namespace Gigya.Microdot.Interfaces.Events
{    
    /// <summary>Indicates this field should be written to flume events, and what name to use for the field name.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]    
    public class FlumeFieldAttribute : Attribute
    {

        /// <summary>A default value to write in case the field or property is null (e.g. "" or 0).</summary>
        public object DefaultValue = null;

        public readonly string Name;
        public bool OnlyForAudit = false;
        public bool OmitFromAudit = false;

        /// <summary>Whether the field value should be encrypted.</summary>
        public bool Encrypt = false;

        /// <summary>Will append "_i", "_f", etc to the name of fields based on their types when enumerating dictionaries.
        /// Cannot be used along with <see cref="Encrypt"/>, since encrypted tags are by definition always strings.</summary>
        public bool AppendTypeSuffix;

        public FlumeFieldAttribute(string _name, object default_value = null)
        {
            this.Name = _name;
            this.DefaultValue = default_value;
        }
    }
}
