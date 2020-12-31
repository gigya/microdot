using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Events
{

    public class EventSerializationTests
    {
        private static readonly CurrentApplicationInfo AppInfo = new CurrentApplicationInfo(nameof(EventSerializationTests), Environment.UserName, Dns.GetHostName());


        EventSerializer SerializerWithStackTrace { get; } = new EventSerializer(() => new EventConfiguration(), new NullEnvironment(),
            new StackTraceEnhancer(
            () => new StackTraceEnhancerSettings(), 
                new NullEnvironment(), 
                AppInfo,
                new JsonExceptionSerializationSettings(()=> new ExceptionSerializationConfig(false, false))
            ),
            () => new EventConfiguration(), 
            AppInfo);

        EventSerializer SerializerWithoutStackTrace { get; } = 
            new EventSerializer(
                () => new EventConfiguration
                {
                        ExcludeStackTraceRule = new Regex(".*")
                }, 
                new NullEnvironment(),
                new StackTraceEnhancer(
                    () => new StackTraceEnhancerSettings(),
                    new NullEnvironment(), 
                    AppInfo,
                    new JsonExceptionSerializationSettings(()=> new ExceptionSerializationConfig(false, false))
                ),
                () => new EventConfiguration(), 
                AppInfo);

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Environment.SetEnvironmentVariable("GIGYA_SERVICE_INSTANCE_NAME",null);
        }

        [Test]
        public async Task PublishExceptionEvent_WhileShouldExcludeStackTraceForFlume()
        {
            var evt = new ServiceCallEvent
            {
                Exception = new ArgumentException("Test Test").ThrowAndCatch()
            };

            var serializedEvent = SerializerWithoutStackTrace.Serialize(evt).ToDictionary(_ => _.Name, _ => _.Value);

            serializedEvent.ShouldContainKey(EventConsts.exMessage);
            serializedEvent[EventConsts.exMessage].ShouldBe("Test Test");

            serializedEvent.ShouldContainKey(EventConsts.exOneWordMessage);
            serializedEvent[EventConsts.exOneWordMessage].ShouldBe("Test_Test");

            serializedEvent.ShouldContainKey(EventConsts.exType);
            serializedEvent[EventConsts.exType].ShouldBe(typeof(ArgumentException).FullName);

            serializedEvent.ShouldNotContainKey(EventConsts.exInnerMessages);
            serializedEvent.ShouldNotContainKey(EventConsts.exStackTrace);
        }


        [Test]
        public async Task PublishExceptionEvent_WithNotTrownException()
        {
            var evt = new Event { Exception = new Exception("Test Test") };
            var serializedEvent = SerializerWithoutStackTrace.Serialize(evt).ToDictionary(_ => _.Name, _ => _.Value);

            serializedEvent.ShouldContainKey(EventConsts.exMessage);
            serializedEvent[EventConsts.exMessage].ShouldBe("Test Test");

            serializedEvent.ShouldContainKey(EventConsts.exOneWordMessage);
            serializedEvent[EventConsts.exOneWordMessage].ShouldBe("Test_Test");
                                        
            serializedEvent.ShouldContainKey(EventConsts.exType);
            serializedEvent[EventConsts.exType].ShouldBe(typeof(Exception).FullName);

            serializedEvent.ShouldNotContainKey(EventConsts.exInnerMessages);
            serializedEvent.ShouldNotContainKey(EventConsts.exStackTrace);                    
        }


        [Test]
        public async Task PublishExceptionEvent_WithEmptyStackTraceAndInnerException()
        {
            var exception = new Exception("Test Test", new Exception("Inner Exception").ThrowAndCatch());
            exception.Data.Add("details", "details");
            var evt = new Event { Exception = exception };

            var serializedEvent = SerializerWithStackTrace.Serialize(evt).ToDictionary(_ => _.Name, _ => _.Value);

            serializedEvent.ShouldContainKey(EventConsts.exMessage);
            serializedEvent[EventConsts.exMessage].ShouldBe("Test Test");

            serializedEvent.ShouldContainKey(EventConsts.exOneWordMessage);
            serializedEvent[EventConsts.exOneWordMessage].ShouldBe("Test_Test");

            serializedEvent.ShouldContainKey(EventConsts.exInnerMessages);
            serializedEvent[EventConsts.exInnerMessages].ShouldBe("Inner Exception");

            serializedEvent.ShouldContainKey(EventConsts.exType);
            serializedEvent[EventConsts.exType].ShouldBe(typeof(Exception).FullName);

            serializedEvent.ShouldContainKey(EventConsts.exStackTrace);
            serializedEvent[EventConsts.exStackTrace].ShouldNotBeNull();
        }


        [Test]
        public async Task PublishExceptionEventWith_InnerException()
        {
            var exception = new Exception("Test Test", new Exception("Inner Exception")).ThrowAndCatch();
            exception.Data.Add("details", "details");
            var evt = new Event { Exception = exception };

            var serializedEvent = SerializerWithStackTrace.Serialize(evt).ToDictionary(_ => _.Name, _ => _.Value);

            serializedEvent.ShouldContainKey(EventConsts.exMessage);
            serializedEvent[EventConsts.exMessage].ShouldBe("Test Test");

            serializedEvent.ShouldContainKey(EventConsts.exOneWordMessage);
            serializedEvent[EventConsts.exOneWordMessage].ShouldBe("Test_Test");

            serializedEvent.ShouldContainKey(EventConsts.exInnerMessages);
            serializedEvent[EventConsts.exInnerMessages].ShouldBe("Inner Exception");

            serializedEvent.ShouldContainKey(EventConsts.exType);
            serializedEvent[EventConsts.exType].ShouldBe(typeof(Exception).FullName);

            serializedEvent.ShouldContainKey(EventConsts.exStackTrace);
            serializedEvent[EventConsts.exStackTrace].ShouldNotBeNull();
        }


        [Test]
        public async Task PublishClientCallEvent()
        {
            var evt = new ClientCallEvent
            {
                Details               = EventConsts.details,
                ErrCode               = 1,
                Message               = EventConsts.message,
                RequestId             = EventConsts.callID,
                RequestStartTimestamp = 0,
                ResponseEndTimestamp  = 2 * Stopwatch.Frequency,
                TargetHostName        = EventConsts.targetHost,
                TargetMethod          = EventConsts.targetMethod,
                TargetService         = EventConsts.targetService,
                ParentSpanId          = EventConsts.parentSpanID,
                SpanId                = EventConsts.spanID,
                EncryptedTags         = new Dictionary<string, object> { { "EncryptedTag", "EncryptedTagValue" } },
                UnencryptedTags       = new Dictionary<string, object> { { "UnencryptedTag", "UnencryptedTagValue" } }
            };

            var serializedEvent = SerializerWithStackTrace.Serialize(evt).ToDictionary(_ => _.Name, _ => _.Value);

            serializedEvent.ShouldContainKey(EventConsts.type);
            serializedEvent[EventConsts.type].ShouldBe(EventConsts.ClientReqType);

            serializedEvent.ShouldContainKey(EventConsts.callID);
            serializedEvent[EventConsts.callID].ShouldBe(EventConsts.callID);

            serializedEvent.ShouldContainKey(EventConsts.parentSpanID);
            serializedEvent[EventConsts.parentSpanID].ShouldBe(EventConsts.parentSpanID);

            serializedEvent.ShouldContainKey(EventConsts.spanID);
            serializedEvent[EventConsts.spanID].ShouldBe(EventConsts.spanID);

            serializedEvent.ShouldContainKey(EventConsts.statsTotalTime);
            serializedEvent[EventConsts.statsTotalTime].ShouldBe("2000");

            serializedEvent.ShouldContainKey(EventConsts.targetService);
            serializedEvent[EventConsts.targetService].ShouldBe(EventConsts.targetService);

            serializedEvent.ShouldContainKey(EventConsts.targetHost);
            serializedEvent[EventConsts.targetHost].ShouldBe(EventConsts.targetHost);

            serializedEvent.ShouldContainKey(EventConsts.targetMethod);
            serializedEvent[EventConsts.targetMethod].ShouldBe(EventConsts.targetMethod);

            serializedEvent.ShouldContainKey(EventConsts.clnSendTimestamp);
            serializedEvent[EventConsts.clnSendTimestamp].ShouldBe("0");

            serializedEvent.ShouldContainKey(EventConsts.message);
            serializedEvent[EventConsts.message].ShouldBe(EventConsts.message);

            serializedEvent.ShouldContainKey(EventConsts.details);
            serializedEvent[EventConsts.details].ShouldBe(EventConsts.details);
                                        
            serializedEvent.ShouldContainKey(EventConsts.srvSystem);
            serializedEvent[EventConsts.srvSystem].ShouldBe(AppInfo.Name);

            serializedEvent.ShouldContainKey(EventConsts.srvVersion);
            serializedEvent[EventConsts.srvVersion].ShouldBe(AppInfo.Version.ToString(4));

            serializedEvent.ShouldContainKey(EventConsts.infrVersion);
            serializedEvent[EventConsts.infrVersion].ShouldBe(AppInfo.InfraVersion.ToString(4));

            serializedEvent.ShouldContainKey(EventConsts.srvSystemInstance);
                    
            serializedEvent.ShouldContainKey(EventConsts.runtimeHost);
            serializedEvent[EventConsts.runtimeHost].ShouldBe(CurrentApplicationInfo.HostName);

            serializedEvent.ShouldContainKey(EventConsts.tags + ".UnencryptedTag");
            serializedEvent[EventConsts.tags + ".UnencryptedTag"].ShouldBe("UnencryptedTagValue");

            serializedEvent.ShouldContainKey(EventConsts.tags + ".EncryptedTag");
            serializedEvent[EventConsts.tags + ".EncryptedTag"].ShouldNotBeNull();
        }

    }
}