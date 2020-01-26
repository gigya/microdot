using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.LanguageExtensions;
using Gigya.Microdot.SharedLogic.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

namespace Gigya.Microdot.Configuration
{
    [Obsolete("Remove before next major version.")]
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

        /// <summary>
        /// Reads JSON files with the following format: { "key": "value"[, ...] }.
        /// </summary>
        /// <param name="path">The path to file.</param>
        /// <returns>Enumeration of read entries, which can be applied to the environment.</returns>
        public static IEnumerable<Entry> ReadFromJsonFile(string path)
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

        public static void ApplyToEnvironment(IEnumerable<Entry> entries)
        {
            foreach (var e in entries)
                Environment.SetEnvironmentVariable(e.Key, e.Value);
        }
    }
}
