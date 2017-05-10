namespace Gigya.Microdot.Interfaces.Configuration
{
    public class ConfigItemInfo
    {
        public string Value { get; set; }

        public uint Priority { get; set; }

        public string FileName { get; set; }
        
        public override string ToString()
        {
            return $"Value: {Value}, Priority: {Priority}, File: {FileName}";
        }
    }
}
