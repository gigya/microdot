using System;
using System.Collections.Generic;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.SharedLogic.Monitor;
using Metrics;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Monitor
{
    [TestFixture]
    public class PassiveAggregatingHealthCheckTest
    {
        private const string COMPONENT_NAME = "ComponentName";
        private Func<HealthCheckResult> _getHealthResult;
        private DateTimeFake _dateTimeFake;
        private PassiveAggregatingHealthCheck _healthStatus;
        private IHealthMonitor healthMonitor;

        [SetUp]
        public void Setup()
        {
            _dateTimeFake = new DateTimeFake();

            healthMonitor = Substitute.For<IHealthMonitor>();
            healthMonitor.SetHealthFunction(Arg.Any<string>(), Arg.Any<Func<HealthCheckResult>>(), Arg.Any<Func<Dictionary<string, string>>>())
                .Returns(c =>
                {
                    _getHealthResult = c.Arg<Func<HealthCheckResult>>();
                    return new ComponentHealthMonitor(c.Arg<string>(), _getHealthResult);
                });            
        }

        [Test]
        public void UnhealthyOnAllChilds_OneChildInBadState_IsUnhealthy()
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, ReportingStrategy.UnhealthyOnAllChilds);
            _healthStatus.SetBad("", TimeSpan.FromSeconds(1), "badChild");
            _healthStatus.SetGood("", TimeSpan.FromSeconds(1), "goodChild");
            _getHealthResult().IsHealthy.ShouldBe(true);
        }

        [Test]
        public void UnhealthyOnAtLeastOneChild__OneChildInBadState_IsUnhealthy()
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, ReportingStrategy.UnhealthyOnAtLeastOneChild);
            _healthStatus.SetBad("", TimeSpan.FromSeconds(1), "badChild");
            _healthStatus.SetGood("", TimeSpan.FromSeconds(1), "goodChild");
            _getHealthResult().IsHealthy.ShouldBe(false);
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void ChildMessage_Expires(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            _healthStatus.SetGood("details", TimeSpan.FromSeconds(1), "level1");
            var result = _getHealthResult();
            result.Message.ShouldContain("details");

            _dateTimeFake.UtcNow += TimeSpan.FromSeconds(1);
            result = _getHealthResult();
            result.Message.ShouldNotContain("details");
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void MessageShouldContainChildDetails(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            var childDetails = "child details";
            _healthStatus.SetGood(childDetails, TimeSpan.FromSeconds(1), "child1");
            var result = _getHealthResult();
            result.IsHealthy.ShouldBe(true);
            result.Message.ShouldContain(childDetails);
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void Default_IsHealthy(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            _getHealthResult().IsHealthy.ShouldBe(true);
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void BadState_IsUnhealthy(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            _healthStatus.SetBad("", TimeSpan.FromSeconds(1), "");
            _getHealthResult().IsHealthy.ShouldBe(false);
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void ChildState_TurnToGood_IsHealthy(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            _healthStatus.SetBad("", TimeSpan.FromSeconds(1), "child");
            _getHealthResult().IsHealthy.ShouldBe(false);
            _healthStatus.SetGood("", TimeSpan.FromSeconds(1), "child");
            _getHealthResult().IsHealthy.ShouldBe(true);
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void BadState_Expires(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            _healthStatus.SetBad("", TimeSpan.FromSeconds(1), "level1");
            var result = _getHealthResult();
            result.IsHealthy.ShouldBe(false);

            _dateTimeFake.UtcNow += TimeSpan.FromSeconds(1);
            result = _getHealthResult();
            result.IsHealthy.ShouldBe(true);
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void UnhealthyOnAtLeastOneChild_UnsetBadChild_IsHealthy(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            _healthStatus.SetBad("", TimeSpan.FromSeconds(1), "parent", "child");
            _getHealthResult().IsHealthy.ShouldBe(false);

            _healthStatus.Unset("parent", "child");
            _getHealthResult().IsHealthy.ShouldBe(true);
        }

        [Test]
        [TestCase(ReportingStrategy.UnhealthyOnAtLeastOneChild)]
        [TestCase(ReportingStrategy.UnhealthyOnAllChilds)]
        public void ChildExpiry_PospondsParentExpiry(ReportingStrategy reportingStrategy)
        {
            _healthStatus = new PassiveAggregatingHealthCheck(_dateTimeFake, COMPONENT_NAME, healthMonitor, reportingStrategy);
            _healthStatus.SetGood("", TimeSpan.FromSeconds(1), "parent", "child1"); // now parent is in one second delay
            _healthStatus.SetBad("", TimeSpan.FromSeconds(2), "parent", "child2"); // child2 will expire only after two seconds 
            _dateTimeFake.UtcNow += TimeSpan.FromSeconds(1);
            _getHealthResult().IsHealthy.ShouldBe(false);
        }
    }
}
