using System.Collections.Generic;
using System.Linq;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests
{
    [TestFixture()]
    public class RequestOverridesTests
    {
        [Test]
        public void CheckRequestOverridesShallowClone()
        {
            RequestOverrides ro = new RequestOverrides();
            HostOverride ho1 = new HostOverride{Host = "testHost1", Port = 1234, ServiceName = "testService1"};
            ho1.AdditionalProperties = new Dictionary<string, object>();
            ho1.AdditionalProperties.Add("ho1Key", "ho1Value");

            HostOverride ho2 = new HostOverride{ Host = "testHost2", Port = 1235, ServiceName = "testService2"};
            ho2.AdditionalProperties = new Dictionary<string, object>();
            ho2.AdditionalProperties.Add("ho2Key", "ho2Value");

            ro.Hosts = new List<HostOverride>(new []{ho1, ho2});
            ro.PreferredEnvironment = "pe1";
            ro.AdditionalProperties = new Dictionary<string, object>();
            ro.AdditionalProperties.Add("roKey", "roValue");

            RequestOverrides roResult = ro.ShallowCloneWithOverrides("pe2", CacheSuppress.UpToNextServices);
            
            Assert.AreEqual(ro.Hosts.Count, roResult.Hosts.Count);
            Assert.AreEqual(ro.Hosts.Join(roResult.Hosts, h => new {h.Host, h.Port, h.ServiceName}, hr => new {hr.Host, hr.Port, hr.ServiceName}, (h, hr) => hr).Count(), roResult.Hosts.Count);
            Assert.AreEqual(roResult.Hosts[0].AdditionalProperties["ho1Key"], "ho1Value");
            Assert.AreEqual(roResult.Hosts[1].AdditionalProperties["ho2Key"], "ho2Value");
            Assert.AreEqual(roResult.AdditionalProperties["roKey"], "roValue");
            Assert.AreEqual(ro.PreferredEnvironment, "pe1");
            Assert.AreEqual(roResult.PreferredEnvironment, "pe2");
            Assert.AreEqual(roResult.SuppressCaching, CacheSuppress.UpToNextServices);
        }
    }
}
