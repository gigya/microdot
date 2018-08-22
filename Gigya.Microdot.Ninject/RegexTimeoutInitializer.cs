using System;
using System.Configuration;

namespace Gigya.Microdot.Ninject
{
    public interface IRegexTimeoutInitializer
    {
        void Init();
    }
    /// <summary>
    /// Notice that REGEX_DEFAULT_MATCH_TIMEOUT can be set only once and will be determined when calling the first regex the default in infinite!!
    /// </summary>
    public class RegexTimeoutInitializer : IRegexTimeoutInitializer
    {
        public void Init()
        {
            int regexDefaultMachTimeOutSeconds = 10;
            try
            {
                regexDefaultMachTimeOutSeconds = int.Parse(ConfigurationManager.AppSettings["regexDefaultMachTimeOut"]);
            }
            catch (Exception e)
            {
            }

            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT",TimeSpan.FromSeconds(regexDefaultMachTimeOutSeconds));
            Console.WriteLine($"REGEX_DEFAULT_MATCH_TIMEOUT is set to {regexDefaultMachTimeOutSeconds} Seconds");
        }
    }
}