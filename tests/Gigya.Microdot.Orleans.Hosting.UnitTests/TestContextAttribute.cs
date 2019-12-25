using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class TestContextAttribute : Attribute, IApplyToContext
    {
        public void ApplyToContext(TestExecutionContext context)
        {
            Environment.SetEnvironmentVariable("ZONE", "zone");
            Environment.SetEnvironmentVariable("ENV", "env");
        }

        ~TestContextAttribute()
        {
            Environment.SetEnvironmentVariable("ZONE", null);
            Environment.SetEnvironmentVariable("ENV", null);
        }
    }
}
