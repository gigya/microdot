using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.ServiceContract.Exceptions;

namespace Gigya.Microdot.SharedLogic.Exceptions
{
    public class StackTraceEnhancer : IStackTraceEnhancer
    {
        private Func<StackTraceCleanerSettings> GetConfig { get; }
        private IEnvironmentVariableProvider EnvironmentVariableProvider { get; }

        public StackTraceEnhancer(Func<StackTraceCleanerSettings> getConfig, IEnvironmentVariableProvider environmentVariableProvider)
        {
            GetConfig = getConfig;
            EnvironmentVariableProvider = environmentVariableProvider;
        }

        public string AddBreadcrumb(SerializableException exception)
        {
            var breadcrumb = new Breadcrumb
            {
                ServiceName = CurrentApplicationInfo.Name,
                ServiceVersion = CurrentApplicationInfo.Version.ToString(),
                HostName = CurrentApplicationInfo.HostName,
                DataCenter = EnvironmentVariableProvider.DataCenter,
                DeploymentEnvironment = EnvironmentVariableProvider.DeploymentEnvironment
            };

            exception.AddBreadcrumb(breadcrumb);

            return $"--- End of stack trace from {breadcrumb} ---\r\n{exception.StackTrace}";
        }

        public string Clean(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return stackTrace;

            var replacements = GetConfig().RegexReplacements.Values.ToArray();
            var frames = stackTrace
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(f => f.StartsWith("   at System.Runtime") == false && f != "--- End of stack trace from previous location where exception was thrown ---")
                .Select(f => ApplyRegexs(f, replacements));

            return string.Join("\r\n", frames);
        }

        private string ApplyRegexs(string frame, RegexReplace[] replacements)
        {
            string updatedFrame = frame;

            foreach (var regexReplace in replacements)
                updatedFrame = Regex.Replace(updatedFrame, regexReplace.Pattern, regexReplace.Replacement);

            return updatedFrame;
        }
    }

    public class StackTraceCleanerSettings : IConfigObject
    {
        public Dictionary<string, RegexReplace> RegexReplacements { get; set; } = new Dictionary<string, RegexReplace>();
    }

    public class RegexReplace
    {
        public string Pattern { get; set; }
        public string Replacement { get; set; }
    }
}