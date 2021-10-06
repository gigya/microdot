﻿using FluentAssertions;
using Gigya.Common.Application.HttpService.Client;
using Gigya.Microdot.Testing.Shared.Service;
using Metrics;
using Metrics.MetricData;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Gigya.Microdot.UnitTests.ServiceListenerTests
{
    [TestFixture] // there is static in the test don't run in ParallelScope
    public class MetricsTests
    {

        [SetUp]
        public void SetUp()
        {
            Metric.ShutdownContext("Service");
        }


        [TearDown]
        public void TearDown()
        {
            Metric.ShutdownContext("Service");
        }
        
        
        /*
            The test bringing test host with Calculator service and calling to a method 
                1) without exception
                2) with exception
            And counting amount of Calls that Http Service Listener records.

            Chaing of calls:
            
            Gigya.Microdot
                .Hosting.HttpService.HttpServiceListener.HttpServiceListener.Ctor()
                .Ninject.Host.MicrodotServiceHost.OnStart()
                .Hosting.Service.ServiceHostBase.Run()
                .Testing.Shared.Service.NonOrleansServiceTester<Gigya.Microdot.UnitTests.TestingHost<Gigya.Microdot.UnitTests.IDemoService>>
        */

        [Test]
        public void TestMetricsOnSuccess()
        {
            using (var testinghost = new NonOrleansServiceTester<TestingHost<IDemoService>>())
            {
                testinghost.Host.Kernel.Get<IDemoService>().Increment(0).Returns((ulong)1);

                var res = testinghost.GetServiceProxy<IDemoService>().Increment(0).Result;
                res.Should().Be(1);

                testinghost.Host.Kernel.Get<IDemoService>().Received().Increment(0);


                Thread.Sleep(200);
                GetMetricsData(testinghost.Host.ServiceName).AssertEquals(DefaultExpected("Success"));
            }
        }

        [Test]
        public void TestMetricsOnFailure()
        {
            using (var testinghost = new NonOrleansServiceTester<TestingHost<IDemoService>>())
            {
                testinghost.Host.Kernel.Get<IDemoService>().When(a => a.DoSomething()).Do(x => { throw new Exception("Do exception"); });

                Assert.Throws<RemoteServiceException>(() => testinghost.GetServiceProxy<IDemoService>().DoSomething().GetAwaiter().GetResult());
                

                Thread.Sleep(200);
                GetMetricsData(testinghost.Host.ServiceName).AssertEquals(DefaultExpected("Failed"));
            }
        }


        private MetricsData GetMetricsData(string hostName)
        {
            return
                Metric.Context("Service")
                      .Context(hostName)
                      .DataProvider.CurrentMetricsData;
        }


        private static MetricsDataEquatable DefaultExpected(string name)
        {
            return new MetricsDataEquatable
            {
                Counters = new List<MetricDataEquatable>
                {
                    new MetricDataEquatable {Name = name, Unit = Unit.Calls}
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
