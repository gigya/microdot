using System.IO;
using System.Reflection;

using FluentAssertions;
using Gigya.Microdot.SharedLogic.HttpService;
using Newtonsoft.Json;

using NUnit.Framework;

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

        [Test]
        public void AllGood()
        {
            var requestData = new HttpServiceRequest(methodInfo, new object[] {""})
            {
                TracingData = new TracingData
                {
                    RequestID = "1",
                    HostName = "test",
                    ServiceName = "test"
                }
            };
            var requestDataReturned = SerializeDeserialize(requestData);

            requestDataReturned.ShouldBeEquivalentTo(requestData);
        }
        
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