using System;
using System.Configuration;

namespace Gigya.Microdot.Ninject
{
   
    /// <summary>
    /// Notice that REGEX_DEFAULT_MATCH_TIMEOUT can be set only once and will be determined when calling the first regex, the default is infinite!!
    /// </summary>
    /// <remarks>
    /// See <see cref="System.Text.RegularExpressions.Regex.MatchTimeout"/> static property.
    /// </remarks>
    public class RegexTimeoutInitializer 
    {
        public void Init()
        {
            int regexDefaultMatchTimeOutMs = (int) TimeSpan.FromSeconds(10).TotalMilliseconds;
            try
            {
                if (ConfigurationManager.AppSettings["regexDefaultMatchTimeOutMs"] != null)
                {
                    regexDefaultMatchTimeOutMs = int.Parse(ConfigurationManager.AppSettings["regexDefaultMatchTimeOutMs"]);
                }
            }
            catch (Exception e)
            {
            }

            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(regexDefaultMatchTimeOutMs));
            Console.WriteLine($"REGEX_DEFAULT_MATCH_TIMEOUT is set to {regexDefaultMatchTimeOutMs} ms");
        }
    }
}