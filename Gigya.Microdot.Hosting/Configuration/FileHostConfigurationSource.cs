using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.LanguageExtensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace Gigya.Microdot.Hosting.Configuration
{

    public sealed class FileHostConfigurationSource : IHostConfigurationSource
    {
        public CurrentApplicationInfo ApplicationInfo { get; }

        public string Zone => GetOrNull(nameof(Zone));
        public string Region => GetOrNull(nameof(Region));
        public string DeploymentEnvironment => GetOrNull(nameof(DeploymentEnvironment));
        public string ConsulAddress => GetOrNull(nameof(ConsulAddress));

        public DirectoryInfo ConfigRoot => GetOrNull(nameof(ConfigRoot))?.To(x => new DirectoryInfo(x));
        public FileInfo LoadPathsFile => GetOrNull(nameof(LoadPathsFile))?.To(x => new FileInfo(x));

        public IDictionary<string, string> CustomKeys => new Dictionary<string, string>();

        #region Entries
        private readonly Dictionary<string, Entry> entries;

        private string GetOrNull(string key)
        {
            if (entries.TryGetValue(key, out var val))
                return val.Value;
            return null;
        }
        #endregion

        public FileHostConfigurationSource(string path)
        {
            entries = ReadFromJsonFile(path).ToDictionary(x => x.Key);
        }

        private sealed class Entry
        {
            public string Key { get; }
            public string Value { get; }

            public Entry(string key, string value)
            {
                this.Key = key.NullWhenEmpty() ?? throw new ArgumentNullException(nameof(key));
                this.Value = value;
            }
        }

        private static IEnumerable<Entry> ReadFromJsonFile(string path)
        {
            JObject envVarsObject;

            try
            {
                var text = File.ReadAllText(path);

                if (string.IsNullOrEmpty(text))
                    return Enumerable.Empty<Entry>();

                envVarsObject = JObject.Parse(text);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException($"Missing or invalid configuration file: {path}", ex);
            }

            if (envVarsObject == null)
                return Enumerable.Empty<Entry>();

            return
                envVarsObject
                .Properties()
                .Where(a => a.HasValues)
                .Select(x => new Entry(x.Name, x.Value.Value<string>()))
                .ToArray();
        }
    }
}
