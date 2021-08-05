using System.Threading.Tasks;
using Gigya.Microdot.UnitTests.ServiceProxyTests;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests
{
    public class LoopTester
    {
        [Test]
        public async Task RunTestInLoop()
        {
            for (var i = 0; i < 1000; i++)
            {
                var test = new JsonExceptionSerializerTests();

                await test.OneTimeSetupAsync();
                test.Setup();

                test.RequestException_Serialization_AddBreadcrumbs();

                test.TearDown();
                test.OneTimeTearDown();
            }

        }
    }
}