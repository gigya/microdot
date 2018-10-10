using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Measurement;
using NUnit.Framework;
using FluentAssertions;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Events
{
    public class StatsEventTests
    {
        [Test]
        public async Task ProcessingTime_TotalTimeIsNull_Null()
        {            
            var statsEvent = new MockStatsEvent();            
            statsEvent.ProcessingTime.ShouldBeNull();
        }

        [Test]
        public async Task ProcessingTime_ServicesTimeIsNull_Null()
        {
            var statsEvent = new MockStatsEvent();
            using (RequestTimings.Current.Request.Measure()) { }

            statsEvent.ProcessingTime.ShouldBeNull();
        }

        [Test]
        public async Task ProcessingTime_TotalTimeAndServicesTimeHaveValues_TotalTimeMinusServicesTime()
        {
            var statsEvent = new MockStatsEvent();
            using (RequestTimings.Current.Request.Measure()) { }

            using (RequestTimings.Current.ServicesCallsDictionary["Service1"].Measure()) {}
            using (RequestTimings.Current.ServicesCallsDictionary["Service2"].Measure()) {}

            var expected = RequestTimings.Current.Request.ElapsedMS - RequestTimings.Current.ServicesCallsDictionary["Service1"].ElapsedMS - RequestTimings.Current.ServicesCallsDictionary["Service2"].ElapsedMS;

            statsEvent.ProcessingTime.Should().BeApproximately(expected.Value, 0.0001);
        }
    }

    internal class MockStatsEvent : StatsEvent
    {

    }
}
