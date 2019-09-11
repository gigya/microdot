using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.Monitor;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public class SomeGrain
    {
        private Func<BusSettings> BusSettingsFactory { get; }


        public SomeGrain(Func<BusSettings> busSettingsFactory)
        {
            BusSettingsFactory = busSettingsFactory;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var busSettings = BusSettingsFactory();
            sw.Stop();
            var elapsed = sw.Elapsed;
        }


        public void DoWork()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var busSettings = BusSettingsFactory();
            sw.Stop();
            var elapsed = sw.Elapsed;
        }
    }

    public class TypedConfigTest
    {
        private DateTime dateTime = new DateTime(2016, 11, 8, 15, 57, 20);

        [Test]
        public void Test_GetConfigObject()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "il3-_env_func-infraTest"},
                {"BusSettings.MessageFormatNullable", "Json"},
                {"BusSettings.RequestTimeoutInMs", "30000"},
                {"BusSettings.RequestTimeoutInMsNullable", "30000"},
                {"BusSettings.MessageFormat", "Json"},
                {"BusSettings.Date", "2016/11/08"},
                {"BusSettings.DateTime", "2016-11-08 15:57:20"},
            });
            
            var kafkaSettings = infraKernel.Get<SomeGrain>();

            kafkaSettings.DoWork();

            infraKernel.Dispose();
        }


        [Test]
        public async Task OnlyValueCausesNotification()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "il3-_env_func-infraTest"},
                {"BusSettings.MessageFormatNullable", "Json"},
            });

            var notifications = infraKernel.Get<ISourceBlock<BusSettings>>();
            var configItems = infraKernel.Get<OverridableConfigItems>();
            var eventSource = infraKernel.Get<ManualConfigurationEvents>();

            BusSettings configFromNotification = null;
            notifications.LinkTo(new ActionBlock<BusSettings>(set =>
                                                              {
                                                                  configFromNotification = set;
                                                              }));

            var extractor = infraKernel.Get<Func<BusSettings>>();
            var busSettings = extractor();
            busSettings.TopicName.ShouldBe("il3-_env_func-infraTest");

            configFromNotification = null;
            configItems.SetValue("BusSettings.MessageFormatNullable", "Invalid");
            eventSource.RaiseChangeEvent();
            await Task.Delay(100);
            configFromNotification.ShouldBeNull();

            configFromNotification = null;
            configItems.SetValue("BusSettings.MessageFormatNullable", "Json");
            eventSource.RaiseChangeEvent();
            await Task.Delay(100);
            configFromNotification.ShouldNotBeNull();
        }


        [Test]
        public async Task NotificationChange()
        {

            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "il3-_env_func-infraTest"},
                {"BusSettings.MessageFormatNullable", "Json"},
                {"BusSettings.RequestTimeoutInMs", "30000"},
                {"BusSettings.RequestTimeoutInMsNullable", "30000"},
                {"BusSettings.MessageFormat", "Json"},
                {"BusSettings.Date", "2016/11/08"},
                {"BusSettings.DateTime", "2016-11-08 15:57:20"},
            });

            var notifications = infraKernel.Get<ISourceBlock<BusSettings>>();
            BusSettings configFromNotification = null;
            notifications.LinkTo(new ActionBlock<BusSettings>(set =>
                {
                    configFromNotification = set;
                }));

            var extractor = infraKernel.Get<Func<BusSettings>>();
            var busSettings = extractor();
            busSettings.TopicName.ShouldBe("il3-_env_func-infraTest");

            var configItems = infraKernel.Get<OverridableConfigItems>();
            var eventSource = infraKernel.Get<ManualConfigurationEvents>();
            configItems.SetValue("BusSettings.TopicName", "Changed");
            eventSource.RaiseChangeEvent();
            await Task.Delay(100);
            configFromNotification.ShouldNotBeNull();
            configFromNotification.TopicName.ShouldBe("Changed");
            infraKernel.Dispose();
        }


        [Test]
        public async Task ObjectValuesChangedAfterUpdate()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "OldValue"},
                {"BusSettings.MessageFormatNullable", "Json"},
                {"BusSettings.RequestTimeoutInMs", "30000"},
                {"BusSettings.RequestTimeoutInMsNullable", "30000"},
                {"BusSettings.MessageFormat", "Json"},
                {"BusSettings.Date", "2016/11/08"},
                {"BusSettings.DateTime", "2016-11-08 15:57:20"},
            });

            var extractor = infraKernel.Get<Func<BusSettings>>();
            var configItems = infraKernel.Get<OverridableConfigItems>();
            var eventSource = infraKernel.Get<ManualConfigurationEvents>();

            var busSettings = extractor();
            busSettings.TopicName.ShouldBe("OldValue");
                        
            configItems.SetValue("BusSettings.TopicName","NewValue");
            
            eventSource.RaiseChangeEvent();
            await Task.Delay(100);
            busSettings = extractor();
            busSettings.TopicName.ShouldBe("NewValue");

            infraKernel.Dispose();
        }

        [Test]
        public async Task ConfigObjectValuesChangedAfterUpdate()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "OldValue"},
            });

            var manualConfigurationEvents = infraKernel.Get<ManualConfigurationEvents>();
            var configItems = infraKernel.Get<OverridableConfigItems>();
            var configFactory = infraKernel.Get<IConfiguration>();

            var busSettings = configFactory.GetObject<BusSettings>();
            busSettings.TopicName.ShouldBe("OldValue");

            configItems.SetValue("BusSettings.TopicName", "NewValue");
            busSettings = await manualConfigurationEvents.ApplyChanges<BusSettings>();
            busSettings.TopicName.ShouldBe("NewValue");

            busSettings = configFactory.GetObject<BusSettings>();
            busSettings.TopicName.ShouldBe("NewValue");

            infraKernel.Dispose();
        }

        [Test]
        public void DeepNestedShouldWork()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>  {
               {"FirstLevel.NextLevel.NextLevel.ID", "ID"},                
            });

            var extractor = infraKernel.Get<Func<FirstLevel>>();
            var deep = extractor();
            deep?.NextLevel?.NextLevel?.ID?.ShouldBe("ID");

            infraKernel.Dispose();
        }

        [Test]
        public void DeepNestedShouldThrowOnValidateError()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>  {
               {"FirstLevel.NextLevel.NextLevel.Name", "Name"},
            });

            var extractor = infraKernel.Get<Func<FirstLevel>>();
            Should.Throw<ConfigurationException>(() => extractor());

            infraKernel.Dispose();
        }

        [Test]
        public void NoConfigurationDontThrowException()
        {
            using (var infraKernel = new TestingKernel<ConsoleLog>())
            {
                infraKernel.Get<FirstLevel>();
            }
        }

        /// <summary>
        /// When configuration changes to a broken one, no exception should be thrown.
        /// Instead the heath monitor should be notified.
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task ChangeToBrokenConfiguration()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "OldValue"},
                {"BusSettings.RequestTimeoutInMs", "30000"},
            });

            var extractor = infraKernel.Get<Func<BusSettings>>();
            var configItems = infraKernel.Get<IConfigItemsSource>() as OverridableConfigItems;
            var eventSource = infraKernel.Get<IConfigurationDataWatcher>() as ManualConfigurationEvents;

            //Make sure a good configuration have been parsed.
            var busSettings = extractor();
            busSettings.RequestTimeoutInMs.ShouldBe(30000);

            configItems.SetValue("BusSettings.RequestTimeoutInMs", "NotNumber");

            eventSource.RaiseChangeEvent();
            await Task.Delay(100);

            //Make sure last good configuration is returned.
            busSettings = extractor();
            busSettings.RequestTimeoutInMs.ShouldBe(30000);

            const string healthComponentName = "Configuration";
            var healthMonitor = (FakeHealthMonitor) infraKernel.Get<IHealthMonitor>();
            healthMonitor.Monitors.ShouldContainKey(healthComponentName);
            healthMonitor.Monitors[healthComponentName]().IsHealthy.ShouldBe(false);

            infraKernel.Dispose();
        }

        [Test]
        public void ShouldThrowOnValidateError()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>  {
                {"BusSettings.ConsumerSettings.ConsumerId", "consID"},
            });

            var extractor = infraKernel.Get<Func<BusSettings>>();
          
            var ex = Should.Throw<ConfigurationException>(() => extractor());
            ex.UnencryptedTags["ValidationErrors"].ShouldNotBeNullOrEmpty();
            infraKernel.Dispose();
        }

        [Test]
        public void AllPropertiesShouldBeSet()
        {
           
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>  {
                {"BusSettings.TopicName", "il3-_env_func-infraTest"},
                {"BusSettings.MessageFormatNullable", "Json"},
                {"BusSettings.RequestTimeoutInMs", "30000"},
                {"BusSettings.RequestTimeoutInMsNullable", "30000"},
                {"BusSettings.MessageFormat", "Json"},
                {"BusSettings.DateTime", "2016-11-08 15:57:20"},
                {"BusSettings.Date", "2016/11/08"},
                {"BusSettings.DateTimeNullable", "2016-11-08 15:57:20"},
                {"BusSettings.ConsumerSettings.ConsumerId", "consID"},
                {"BusSettings.ConsumerSettings.ConsumerName", "consName"},
                {"BusSettings.ConsumerSettings.Country.CountryCode", "US"},                
            });

            var extractor = infraKernel.Get<Func<BusSettings>>();

            var busSettings = extractor();
            busSettings.TopicName.ShouldBe("il3-_env_func-infraTest");
            busSettings.MessageFormatNullable.ShouldBe(MessageFormat.Json);
            busSettings.MessageFormat.ShouldBe(MessageFormat.Json);
            busSettings.RequestTimeoutInMs.ShouldBe(30000);
            busSettings.RequestTimeoutInMsNullable.ShouldBe(30000);
            busSettings.Date.ShouldBe(dateTime.Date);
            busSettings.DateTime.ShouldBe(dateTime);
            busSettings.DateTimeNullable.ShouldBe(dateTime);
            busSettings.ConsumerSettings.ShouldNotBeNull();
            busSettings.ConsumerSettings.ConsumerId.ShouldBe("consID");

            busSettings.ConsumerSettings.Country.ShouldNotBeNull();
            busSettings.ConsumerSettings.Country.CountryCode.ShouldBe("US");

            infraKernel.Dispose();
        }


        [Test]
        public void AllPropertiesExceptNullableShouldBeSet()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "il3-_env_func-infraTest"},
                {"BusSettings.RequestTimeoutInMs", "30000"},
                {"BusSettings.MessageFormat", "Json"},
                {"BusSettings.DateTime", "2016-11-08 15:57:20"},
                {"BusSettings.Date", "2016/11/08"},
                {"BusSettings.TimeSpan", "00:00:01"},
                {"BusSettings.Regex", "/ab+c/"},
                {"BusSettings.Uri", "http://data.com"},
            });
            var extractor = infraKernel.Get<Func<BusSettings>>();
            
            var busSettings = extractor();
            busSettings.TopicName.ShouldBe("il3-_env_func-infraTest");
            busSettings.MessageFormatNullable.ShouldBeNull();
            busSettings.MessageFormat.ShouldBe(MessageFormat.Json);
            busSettings.RequestTimeoutInMs.ShouldBe(30000);
            busSettings.RequestTimeoutInMsNullable.ShouldBeNull();
            busSettings.Date.ShouldBe(dateTime.Date);
            busSettings.DateTime.ShouldBe(dateTime);
            busSettings.TimeSpan.ShouldBe(TimeSpan.FromSeconds(1));
            busSettings.Regex.ShouldNotBeNull();
            busSettings.Uri.ShouldBe(new Uri("http://data.com"));
            infraKernel.Dispose();
        }


        [Test]
        public void NotExistingObjectShouldReturnDefaultValues()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"Kuku", "infraTest"},
            });
            var extractor = infraKernel.Get<Func<GatorConfig>>();

            var gatorConfig = extractor();
            gatorConfig.Name.ShouldBe("Default");
        }

        [Test]
        public void CanReadWithAppendedPrefix()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"Prefix1.Prefix2.GatorConfig.Name", "infraTest"},
            });
            var extractor = infraKernel.Get<Func<GatorConfig>>();

            var busSettings = extractor();
            busSettings.ShouldNotBeNull();
            busSettings.Name.ShouldBe("infraTest");

            infraKernel.Dispose();
        }

        [Test]
        public void CanReadWithReplacingPrefixWInWrongCase()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"Prefix1.Prefix2.GatorConfig.Name", "infraTest"},
            });
            var extractor = infraKernel.Get<Func<GatorLowerCase>>();

            var busSettings = extractor();
            busSettings.ShouldNotBeNull();
            busSettings.Name.ShouldBe("infraTest");

            infraKernel.Dispose();
        }

        [Test]
        public void CanReadWithReplacingPrefix()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"Prefix1.Prefix2.GatorConfig.Name", "infraTest"},
            });
            var extractor = infraKernel.Get<Func<Gator>>();

            var busSettings = extractor();
            busSettings.ShouldNotBeNull();
            busSettings.Name.ShouldBe("infraTest");

            infraKernel.Dispose();
        }  

        //[Test]
        [Ignore("For now it looks like this check is too strict.")]
        public void ShouldThrowOnMissingInt()
        {
            var infraKernel = new TestingKernel<ConsoleLog>(mockConfig: new Dictionary<string, string>
            {
                {"BusSettings.TopicName", "il3-_env_func-infraTest"},                
                {"BusSettings.MessageFormat", "Json"}
            });
            var extractor = infraKernel.Get<Func<BusSettings>>();

            Should.Throw<ConfigurationException>(() => extractor());

            infraKernel.Dispose();
        }
    }
}
