using System;
using System.Configuration;

namespace Gigya.Microdot.Ninject
{
   
    /// <summary>
    /// Notice that REGEX_DEFAULT_MATCH_TIMEOUT can be set only once and will be determined when calling the first regex the default in infinite!!
    /// </summary>
    public class RegexTimeoutInitializer 
    {
        public void Init()
        {
            int regexDefaultMachTimeOutMs =(int) TimeSpan.FromSeconds(10).TotalMilliseconds;
            try
            {
                regexDefaultMachTimeOutMs = int.Parse(ConfigurationManager.AppSettings["regexDefaultMachTimeOutMs"]);
            }
            catch (Exception e)
            {
            }

            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT",TimeSpan.FromMilliseconds(regexDefaultMachTimeOutMs));
            Console.WriteLine($"REGEX_DEFAULT_MATCH_TIMEOUT is set to {regexDefaultMachTimeOutMs} ms");
        }
    }
}