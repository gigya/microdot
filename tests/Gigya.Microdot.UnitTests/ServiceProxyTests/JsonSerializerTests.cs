using Gigya.Common.Application.HttpService.Client;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Security;
using Gigya.Microdot.SharedLogic.Utils;
using Newtonsoft.Json;
using Ninject;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{     
    public class JsonSerializerTests : UpdatableConfigTests
    {
        private IMicrodotTypePolicySerializationBinder SerializationBinder { get; set; }

        [OneTimeSetUp]
        public async Task OneTimeSetupAsync()
        {
            base.OneTimeSetUp();

            SerializationBinder = _unitTestingKernel.Get<IMicrodotTypePolicySerializationBinder>();
        }

        protected override Action<IKernel> AdditionalBindings()
        {
            return null;
        }
    
        [Test]        
        public async Task SerializationBinderUsedToHandleEmptyPartition()
        {                        
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.None,
                SerializationBinder = SerializationBinder
            };
                        
            var serialized = JsonConvert.SerializeObject(new Bar()
            {
                Bars    = Enumerable.Empty<Bar>(),
                Doubles = Enumerable.Empty<double>(),
                Bools = Enumerable.Empty<bool>(),
                Floats = Enumerable.Empty<float>(),
                Ints = Enumerable.Empty<int>(),
                Longs = Enumerable.Empty<long>(),
                Strings = Enumerable.Empty<string>(),
                HashSet = new HashSet<string>() 

            }, settings);

            serialized.ShouldNotContain("EmptyPartition"); //net4 does not contain EmptyPartition & net6 handled by SerializationBinder  

            var newObj = JsonConvert.DeserializeObject<Bar>(serialized, settings);

            Assert.IsEmpty(newObj.Bars);
            Assert.IsEmpty(newObj.Doubles);
            Assert.IsEmpty(newObj.Bools);
            Assert.IsEmpty(newObj.Floats);
            Assert.IsEmpty(newObj.Ints);
            Assert.IsEmpty(newObj.Longs);
            Assert.IsEmpty(newObj.Strings);
            Assert.IsEmpty(newObj.HashSet);
        }

        [Test]
        public async Task SerializationBinderIsNotUsed()
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                DateParseHandling = DateParseHandling.None,
            };

            var serialized = JsonConvert.SerializeObject(new Bar()
            {
                Bars = Enumerable.Empty<Bar>(),
                Doubles = Enumerable.Empty<double>(),
                Bools = Enumerable.Empty<bool>(),
                Floats = Enumerable.Empty<float>(),
                Ints = Enumerable.Empty<int>(),
                Longs = Enumerable.Empty<long>(),
                Strings = Enumerable.Empty<string>(),
                HashSet = new HashSet<string>()
            }, settings);

#if NET5_0_OR_GREATER

            //because SerializationBinder is not used
            int count = serialized.Split("EmptyPartition").Length - 1;
            count.ShouldBe(7);            

#endif

            var newObj = JsonConvert.DeserializeObject<Bar>(serialized, settings);

            Assert.IsEmpty(newObj.Bars);
            Assert.IsEmpty(newObj.Doubles);
            Assert.IsEmpty(newObj.Bools);
            Assert.IsEmpty(newObj.Floats);
            Assert.IsEmpty(newObj.Ints);
            Assert.IsEmpty(newObj.Longs);
            Assert.IsEmpty(newObj.Strings);
            Assert.IsEmpty(newObj.HashSet);
        }
    }


    public class Bar
    {
        public IEnumerable<Bar>    Bars;
        public IEnumerable<double> Doubles;
        public IEnumerable<int> Ints;
        public IEnumerable<string> Strings;
        public IEnumerable<bool> Bools;
        public IEnumerable<float> Floats;
        public IEnumerable<long> Longs;
        public HashSet<string> HashSet;
    }    
}
