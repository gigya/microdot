using System.Linq;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class AdditionalDataSerializationTests
    {
        [Test]
        public void TracingDataShouldContainAdditionaProperties()
        {
            TracingData tData = new TracingData
                                    {
                                        ParentSpanID = "ParentSpanID1",
                                        SpanID = "SpanID1"
            };

            JObject jObject = JObject.FromObject(tData);
            TracingData tDataResult = DeserializeObject<TracingData>(jObject);

            tDataResult.ParentSpanID.ShouldBe("ParentSpanID1");
            tDataResult.SpanID.ShouldBe("SpanID1");
            tDataResult.AdditionalProperties.ShouldBeNull();

            jObject.Add("TracingData", "AdditionalData1");
            tDataResult = DeserializeObject<TracingData>(jObject);

            tDataResult.ParentSpanID.ShouldBe("ParentSpanID1");
            tDataResult.SpanID.ShouldBe("SpanID1");
            tDataResult.AdditionalProperties.ShouldContainKeyAndValue("TracingData", "AdditionalData1");
        }

        [Test]
        public void TracingDataShouldNotContainAdditionaProperties()
        {

            TracingData tData = new TracingData
            {
                ParentSpanID = "ParentSpanID1",
                SpanID = "SpanID1"
            };

            JObject jObject = JObject.FromObject(tData);
            TracingData tDataResult = DeserializeObject<TracingData>(jObject);

            tDataResult.ParentSpanID.ShouldBe("ParentSpanID1");
            tDataResult.SpanID.ShouldBe("SpanID1");
            tDataResult.AdditionalProperties.ShouldBeNull();

            jObject.Add("TracingData", "AdditionalData1");
            tDataResult = DeserializeObject<TracingDataFake>(jObject);

            tDataResult.ParentSpanID.ShouldBe("ParentSpanID1");
            tDataResult.SpanID.ShouldBe("SpanID1");
            ((TracingDataFake)tDataResult).TracingData.ShouldBe("AdditionalData1");
            tDataResult.AdditionalProperties.ShouldBeNull();
        }

        [Test]
        public void HttpServiceRequestShouldContainAdditionaPropertiesRecursively()
        {
            TracingData tData = new TracingData
            {
                ParentSpanID = "ParentSpanID1",
                SpanID = "SpanID1"
            };
            RequestOverrides requestOverrides = new RequestOverrides
            {
                Hosts = new[] { new HostOverride { ServiceName = "Service1", Host = "HostNameOverride" } }.ToList(),
                SuppressCaching = CacheSuppress.RecursiveAllDownstreamServices
            };
            InvocationTarget invocationTarget = new InvocationTarget{MethodName = "MethodName1" };

            HttpServiceRequest serviceRequest = new HttpServiceRequest
            { 
                TracingData = tData,
                Overrides = requestOverrides,
                Target = invocationTarget
            };

            dynamic jObject = JObject.FromObject(serviceRequest);
            HttpServiceRequest serviceRequestResult = DeserializeObject<HttpServiceRequest>(jObject);

            AssertShouldBeNull(serviceRequestResult);

            jObject.Add("ServiceRequestData", "ServiceRequestData1");
            jObject["TracingData"].Add("TracingData", "TracingData1");
            jObject["Overrides"].Add("OverridesData", "OverridesData1");
            jObject["Overrides"]["Hosts"][0].Add("HostOverrideData", "HostOverrideData1");
            jObject["Target"].Add("TargetData", "TargetData1");

            serviceRequestResult = DeserializeObject<HttpServiceRequest>(jObject);
            AssertShouldNotBeNull(serviceRequestResult);
        }

        private void AssertShouldBeNull(HttpServiceRequest serviceRequestResult)
        {
            serviceRequestResult.TracingData.ParentSpanID.ShouldBe("ParentSpanID1");
            serviceRequestResult.TracingData.SpanID.ShouldBe("SpanID1");
            serviceRequestResult.TracingData.AdditionalProperties.ShouldBeNull();

            serviceRequestResult.Overrides.Hosts[0].Host.ShouldBe("HostNameOverride");
            serviceRequestResult.Overrides.Hosts[0].AdditionalProperties.ShouldBeNull();
            serviceRequestResult.Overrides.AdditionalProperties.ShouldBeNull();

            serviceRequestResult.Target.MethodName.ShouldBe("MethodName1");
            serviceRequestResult.Target.AdditionalProperties.ShouldBeNull();

            serviceRequestResult.AdditionalProperties.ShouldBeNull();
        }

        private void AssertShouldNotBeNull(HttpServiceRequest serviceRequestResult)
        {
            serviceRequestResult.TracingData.ParentSpanID.ShouldBe("ParentSpanID1");
            serviceRequestResult.TracingData.SpanID.ShouldBe("SpanID1");
            serviceRequestResult.TracingData.AdditionalProperties.Count.ShouldBe(1);
            serviceRequestResult.TracingData.AdditionalProperties.ShouldContainKeyAndValue("TracingData", "TracingData1");

            serviceRequestResult.Overrides.Hosts[0].Host.ShouldBe("HostNameOverride");
            serviceRequestResult.Overrides.Hosts[0].AdditionalProperties.Count.ShouldBe(1);
            serviceRequestResult.Overrides.Hosts[0].AdditionalProperties.ShouldContainKeyAndValue("HostOverrideData", "HostOverrideData1");
            serviceRequestResult.Overrides.AdditionalProperties.Count.ShouldBe(1);
            serviceRequestResult.Overrides.AdditionalProperties.ShouldContainKeyAndValue("OverridesData", "OverridesData1");
            serviceRequestResult.Overrides.SuppressCaching.ShouldBe(CacheSuppress.RecursiveAllDownstreamServices);

            serviceRequestResult.Target.MethodName.ShouldBe("MethodName1");
            serviceRequestResult.Target.AdditionalProperties.Count.ShouldBe(1);
            serviceRequestResult.Target.AdditionalProperties.ShouldContainKeyAndValue("TargetData", "TargetData1");

            serviceRequestResult.AdditionalProperties.ShouldContainKeyAndValue("ServiceRequestData", "ServiceRequestData1");
        }
        private T DeserializeObject<T>(JObject jObject)
        {

            T tDataResult = JsonConvert.DeserializeObject<T>(jObject.ToString());

            return tDataResult;
        }

        private class TracingDataFake : TracingData
        {
            public string TracingData { get; set; }
        }
    }
}
