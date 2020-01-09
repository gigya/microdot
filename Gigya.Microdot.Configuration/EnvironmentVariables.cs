using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Gigya.Microdot.Configuration
{
    public static class EnvironmentVariables
    {
        public sealed class Entry
        {
            public string Key { get; }
            public string Value { get; }

            public Entry(string key, string value)
            {
                this.Key = key.NullWhenEmpty() ?? throw new ArgumentNullException(nameof(key));
                this.Value = value;
            }
        }

        public static IEnumerable<Entry> ReadFromFile(IFileSystem fileSystem, string path)
        {
            JObject envVarsObject;

            try
            {
                var text = fileSystem.TryReadAllTextFromFile(path);

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

        public static void Apply(IEnumerable<Entry> entries)
        {
            foreach (var e in entries)
                Environment.SetEnvironmentVariable(e.Key, e.Value);
        }
    }
}
