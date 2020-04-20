using System;
using System.Configuration;

namespace Gigya.Microdot.Ninject
{

    /// <summary>
    /// Notice that REGEX_DEFAULT_MATCH_TIMEOUT can be set only once and will be determined when calling the first regex the default in infinite!!
    /// </summary>
    public class RegexTimeoutInitializer
    {
        static RegexTimeoutInitializer()
        {
            int regexDefaultMachTimeOutMs = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
            /*try
            {
                if (ConfigurationManager.AppSettings["regexDefaultMachTimeOutMs"] != null)
                {
                    regexDefaultMachTimeOutMs = int.Parse(ConfigurationManager.AppSettings["regexDefaultMachTimeOutMs"]);
                }
            }
            catch (Exception e)
            {
            } 
            */
            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(regexDefaultMachTimeOutMs));
        }
        public void Init()
        {
            // make sure our static is initialize 
            // our test is running parallel, we need to make sure it happens in the domain only oncse 
        }
    }
}