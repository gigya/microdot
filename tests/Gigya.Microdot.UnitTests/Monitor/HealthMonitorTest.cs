using System.Collections.Generic;
using System.Linq;

using Gigya.Microdot.SharedLogic.Monitor;

using Metrics;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Monitor
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class HealthMonitorTest
    {
        const string COMPONENT = "Component";
        private HealthMonitor _healthMonitor;


        [SetUp]
        public void Setup()
        {
            _healthMonitor = new HealthMonitor();
        }


        [Test]
        public void SetHealthFunction()
        {
            var expectedHealth = HealthCheckResult.Healthy("Health message");
            _healthMonitor.SetHealthFunction(COMPONENT, ()=>expectedHealth);
            GetHealthResult().ShouldBe(expectedHealth);
        }

        [Test]
        public void ReplaceHealthFunction()
        {
            var expectedHealth = HealthCheckResult.Unhealthy("Unhealthy message");
            _healthMonitor.SetHealthFunction(COMPONENT, () => HealthCheckResult.Healthy("I'm healthy !"));
            _healthMonitor.SetHealthFunction(COMPONENT, () => expectedHealth);            
            GetHealthResult().ShouldBe(expectedHealth);
        }

        [Test]
        public void ClearAllMonitors()
        {
            _healthMonitor.SetHealthFunction(COMPONENT, ()=>HealthCheckResult.Unhealthy());
            _healthMonitor.Dispose();
            bool isComponentStillRegisteredToHealthCheck = HealthChecks.GetStatus().Results.Any(_=>_.Name==COMPONENT);
            isComponentStillRegisteredToHealthCheck.ShouldBeFalse();
        }

        [Test]
        public void SetHealthData()
        {
            var expectedData = new Dictionary<string, string>();
            _healthMonitor.SetHealthFunction(COMPONENT, () => HealthCheckResult.Healthy(), ()=>expectedData);
            var data = _healthMonitor.GetData(COMPONENT);
            data.ShouldBe(expectedData);
        }

        [Test]
        public void GetDataAsEmptyDictionaryIfComponentNotRegistered()
        {
            const string unregisteredComponent = "Unregistered component";
            var data = _healthMonitor.GetData(unregisteredComponent);
            data.ShouldNotBeNull();
            data.Count.ShouldBe(0);
        }

        [Test]
        public void GetDataAsEmptyDictionaryIfComponentNotConfiguredDataFunction()
        {
            _healthMonitor.SetHealthFunction(COMPONENT, ()=>HealthCheckResult.Healthy(), null);
            var data = _healthMonitor.GetData(COMPONENT);
            data.ShouldNotBeNull();
            data.Count.ShouldBe(0);
        }

        private static HealthCheckResult GetHealthResult()
        {
            return HealthChecks.GetStatus().Results.First(_=>_.Name==COMPONENT).Check;
        }
    }
}
