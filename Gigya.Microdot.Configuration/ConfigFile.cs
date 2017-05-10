namespace Gigya.Microdot.Configuration
{
    internal class ConfigFile
    {
        public uint Priority;
        public string FullName;

        public ConfigFile(string filename, uint priority)
        {
            Priority      = priority;
            FullName      = filename; 
        }
    }
}
