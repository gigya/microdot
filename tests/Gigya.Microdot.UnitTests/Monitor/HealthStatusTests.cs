using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Hosting.HttpService;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Monitor
{
    [HttpService(5555)]
    public interface IHealthyService : IHealthStatus { }

    [HttpService(5555)]
    public interface IUnhealthyService : IHealthStatus { }

    [TestFixture]
    public class HealthStatusTests
    {
        [Test]
        public void DuplicateIHealthStatus()
        {
            Should.Throw<ProgrammaticException>(() => new IdentityServiceInterfaceMapper(new[] { typeof(IHealthyService), typeof(IUnhealthyService) }));
        }

        [Test]
        public void OneIHealthStatus()
        {
            var identityServiceInterfaceMapper = new IdentityServiceInterfaceMapper(new[] { typeof(IHealthyService) });

            Assert.AreEqual(typeof (IHealthyService), identityServiceInterfaceMapper.HealthStatusServiceType);
        }
    }
}
