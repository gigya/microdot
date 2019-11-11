using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using NSubstitute;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Logging
{
    [TestFixture]
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class OrleansLogAdapterTests
    {
        [Test]
        public void Log_StateWithValues_ShouldCallLogImplementation()
        {
            string category = "category";
            ILog log = Substitute.For<ILog>();
            ILog LogImplementation(string str) => log;
            OrleansLogEnrichment logEnrichment = new OrleansLogEnrichment();
            OrleansConfig orleansConfig = new OrleansConfig();
            OrleansConfig OrleansConfigFunc() => orleansConfig;
            var orleansLogAdapter = new OrleansLogAdapter(category, LogImplementation, logEnrichment, OrleansConfigFunc);

            EventId eventId = new EventId(42, "my_EventId_Name");
            FormattedLogValues formattedLogValues = new FormattedLogValues("my format {orleansLogKey}", "orleansLogValue");
            string Formatter(FormattedLogValues values, Exception exception) => "formatter result";


            orleansLogAdapter.Log(LogLevel.Error, eventId, formattedLogValues, null, Formatter);
           
            log.Received().Write(
                TraceEventType.Error, 
                Arg.Is<Action<LogDelegate>>(logDelegateAction => CheckLogDelegate(logDelegateAction)), 
                Arg.Any<string>(), 
                Arg.Any<int>(), 
                Arg.Any<string>());
        }

        private bool CheckLogDelegate(Action<LogDelegate> logDelegateAction)
        {
            Type logDelegateActionType = logDelegateAction.Target.GetType();
            var logDelegateActionTypeFields = logDelegateActionType.GetFields();

            foreach (var field in logDelegateActionTypeFields)
            {
                if (field.Name == "logMessage")
                {
                    string logMessage = field.GetValue(logDelegateAction.Target) as string;
                    Assert.AreEqual("formatter result", logMessage);
                }

                if (field.Name == "unencryptedTags")
                {
                    if (field.GetValue(logDelegateAction.Target) is Dictionary<string, object> unencryptedTags)
                    {
                        Assert.AreEqual(unencryptedTags["eventId.Id"], 42);
                        Assert.AreEqual(unencryptedTags["eventId.Name"], "my_EventId_Name");
                        Assert.AreEqual(unencryptedTags["IsOrleansLog"], true);
                        Assert.AreEqual(unencryptedTags["eventHeuristicName"], null);
                        Assert.AreEqual(unencryptedTags["orleansLogKey"], "orleansLogValue");
                    }
                }
            }

            return true;
        }

        [Test]
        public void Log_StateWithValues_ShouldNotAddFormatToTags()
        {
            string category = "category";
            ILog log = Substitute.For<ILog>();
            ILog LogImplementation(string str) => log;
            OrleansLogEnrichment logEnrichment = new OrleansLogEnrichment();
            OrleansConfig orleansConfig = new OrleansConfig();
            OrleansConfig OrleansConfigFunc() => orleansConfig;
            var orleansLogAdapter = new OrleansLogAdapter(category, LogImplementation, logEnrichment, OrleansConfigFunc);

            EventId eventId = new EventId(42, "my_EventId_Name");
            FormattedLogValues formattedLogValues = new FormattedLogValues("my format {orleansLogKey}", "orleansLogValue");
            string Formatter(FormattedLogValues values, Exception exception) => "formatter result";


            orleansLogAdapter.Log(LogLevel.Error, eventId, formattedLogValues, null, Formatter);

            log.Received().Write(
                TraceEventType.Error,
                Arg.Is<Action<LogDelegate>>(logDelegateAction => CheckFormatIsFiltered(logDelegateAction)),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>());
        }

        private bool CheckFormatIsFiltered(Action<LogDelegate> logDelegateAction)
        {
            Type logDelegateActionType = logDelegateAction.Target.GetType();
            var logDelegateActionTypeFields = logDelegateActionType.GetFields();

            foreach (var field in logDelegateActionTypeFields)
            {
                if (field.Name == "unencryptedTags")
                {
                    Assert.AreEqual(field.GetValue(logDelegateAction.Target) is Dictionary<string, object> unencryptedTags && unencryptedTags.ContainsKey("{OriginalFormat}"), false);
                }
            }

            return true;
        }

        [Test]
        public void Log_NullState_ShouldCallLogImplementation()
        {
            string category = "category";
            ILog log = Substitute.For<ILog>();
            ILog LogImplementation(string str) => log;
            OrleansLogEnrichment logEnrichment = new OrleansLogEnrichment();
            OrleansConfig orleansConfig = new OrleansConfig();
            OrleansConfig OrleansConfigFunc() => orleansConfig;
            var orleansLogAdapter = new OrleansLogAdapter(category, LogImplementation, logEnrichment, OrleansConfigFunc);

            EventId eventId = new EventId(42, "my_EventId_Name");

            string Formatter(FormattedLogValues values, Exception exception) => "formatter result";


            orleansLogAdapter.Log<FormattedLogValues>(LogLevel.Error, eventId, null, null, Formatter);

            log.Received().Write(TraceEventType.Error, Arg.Any<Action<LogDelegate>>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>());
        }
    }
}
