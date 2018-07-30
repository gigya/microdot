using System;
using System.Text.RegularExpressions;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Ninject
{
    public class RegexConfig : IConfigObject
    {
        public int TimeoutInSeconds { get; set; } = 10;

    }

    public interface IRegexConfigLoader
    { }


    public class RegexConfigLoader : IRegexConfigLoader
    {
        private readonly RegexConfig _regexConfig;

        public RegexConfigLoader(RegexConfig regexConfig)
        {
            _regexConfig = regexConfig;
            Initialize();
        }

        private void Initialize()
        {
            AppDomain domain = AppDomain.CurrentDomain;
            domain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(_regexConfig.TimeoutInSeconds));
            PopulateRegexWithDefaultTimeout(_regexConfig.TimeoutInSeconds);
        }

        private void PopulateRegexWithDefaultTimeout(int timeoutInSeconds)
        {
            var type = typeof(Regex); // MyClass is static class with static properties
            var p = type.GetField("DefaultMatchTimeout", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            p.SetValue(null, TimeSpan.FromSeconds(timeoutInSeconds));
        }
    }

}