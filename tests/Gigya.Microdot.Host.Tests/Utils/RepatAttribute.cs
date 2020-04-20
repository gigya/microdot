using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit.Sdk;

namespace Gigya.Microdot.Host.Tests.Utils
{
    public class RepeatAttribute : DataAttribute
    {
        private readonly int count;

        public RepeatAttribute(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count),
                      "Repeat count must be greater than 0.");
            }
            this.count = count;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            return Enumerable.Range(0, this.count).Select(x => new object[] { x });
        }
    }
}
