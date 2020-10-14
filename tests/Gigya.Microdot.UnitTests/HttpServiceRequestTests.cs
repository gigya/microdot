using System.IO;
using System.Reflection;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.HttpService;
using Newtonsoft.Json;

using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests {

    public class HttpServiceRequestTests {
        private MethodInfo methodInfo;
        byte[] data;
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto, NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented};
        
        [SetUp]
        public void SetUp() {
            MethodWithOneParam(null);
            data = null;
        }

        private void MethodWithOneParam(string str) {
            methodInfo = (MethodInfo)MethodBase.GetCurrentMethod();
        }
        /*
        [Test]
        public void AllGood()
        {
            var requestData = new HttpServiceRequest(methodInfo, new object[] {""})
            {
                TracingData = new TracingData
                {
                    RequestID = "1",
                    HostName = "test",
                    ServiceName = "test",
                    Tags = new ContextTags()
                    {
                        ["tag1"] = (1, false, true),
                        ["tag2"] = ("dsds", true, false),
                        ["tag3"] = (new Foo(), true, false),
                    }
                }
            };
            var requestDataReturned = SerializeDeserialize(requestData);

            requestDataReturned.TracingData.RequestID.ShouldBe(requestData.TracingData.RequestID);
            requestDataReturned.TracingData.HostName.ShouldBe(requestData.TracingData.HostName);
            requestDataReturned.TracingData.ServiceName.ShouldBe(requestData.TracingData.ServiceName);
            requestDataReturned.TracingData.Tags["tag1"].value.ShouldBe(1);
            requestDataReturned.TracingData.Tags["tag1"].unencryptedLog.ShouldBe(false);
            requestDataReturned.TracingData.Tags["tag1"].encryptedLog.ShouldBe(true);
            requestDataReturned.TracingData.Tags["tag3"].value.ShouldBeOfType<Foo>();
        }*/

        private HttpServiceRequest SerializeDeserialize(HttpServiceRequest requestData) {

            MemoryStream ms = new MemoryStream();
            
            var serializer = JsonSerializer.Create(JsonSettings);
            using(var sw = new StreamWriter(ms))
            {
                serializer.Serialize(sw, requestData);
                sw.Flush();
                if(data == null) {
                    data = ms.ToArray();
                }
            }

            ms = new MemoryStream(data);
            using (var jtr = new JsonTextReader(new StreamReader(ms)))
            {
                return serializer.Deserialize<HttpServiceRequest>(jtr);                
            }
        }
    }
}