using System;
using System.Collections.Generic;
using System.Threading;

using FluentAssertions;

using Gigya.Common.Application.HttpService.Client;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.Testing;

using Metrics;
using Metrics.MetricData;

using Ninject;

using NSubstitute;

using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{
    [TestFixture]
    public class MetricsTests
    {
        private IDemoService _proxyInstance;
        [SetUp]
        public void SetUp()
        {
            Metric.ShutdownContext("Service");

            TracingContext.SetUpStorage();
            TracingContext.SetRequestID("1");

           
            var kernel = new TestingKernel<ConsoleLog>();
            _proxyInstance = kernel.Get<IDemoService>();
        }


        [TearDown]
        public  void TearDown()
        {
            Metric.ShutdownContext("Service");
        }


        [Test]
        public void TestMetricsOnSuccess()
        {
            TestingHost<IDemoService> testinghost = new TestingHost<IDemoService>();
            var task = testinghost.RunAsync();
            testinghost.Instance.Increment(0).Returns((ulong)1);
        
         
            var res = _proxyInstance.Increment(0).Result;
            res.Should().Be(1);

            testinghost.Instance.Received().Increment(0);

        
            testinghost.Stop();
            task.Wait();
            Thread.Sleep(100);
            GetMetricsData().AssertEquals(DefaultExpected());

        }

 
        [Test]
        public void TestMetricsOnFailure()
        {
            TestingHost<IDemoService> testinghost = new TestingHost<IDemoService>();
            var task = testinghost.RunAsync();

            testinghost.Instance.When(a => a.DoSomething()).Do(x => { throw new Exception(); });

            Assert.Throws<RemoteServiceException>(() => _proxyInstance.DoSomething().GetAwaiter().GetResult());

            var metricsExpected = DefaultExpected();

            metricsExpected.Counters = new List<MetricDataEquatable>
            {
                new MetricDataEquatable {Name = "Failed", Unit = Unit.Calls}
            };

            testinghost.Stop();
            task.Wait();

            GetMetricsData().AssertEquals(metricsExpected);
        }


        private static MetricsData GetMetricsData()
        {
            return
                Metric.Context("Service")
                      .Context(CurrentApplicationInfo.Name)
                      .DataProvider.CurrentMetricsData;
        }


        private static MetricsDataEquatable DefaultExpected()
        {
            return new MetricsDataEquatable
            {
                Counters = new List<MetricDataEquatable>
                {
                    new MetricDataEquatable {Name = "Success", Unit = Unit.Calls}
                },
                Timers = new List<MetricDataEquatable>
                {
                    new MetricDataEquatable {Name = "ActiveRequests", Unit = Unit.Requests},
                    new MetricDataEquatable {Name = "Serialization", Unit = Unit.Calls},
                    new MetricDataEquatable {Name = "Deserialization", Unit = Unit.Calls},
                    new MetricDataEquatable {Name = "Roundtrip", Unit = Unit.Calls}
                }
            };
        }
    }
}
