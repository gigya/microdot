using Gigya.Microdot.Interfaces.Configuration;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Gigya.Microdot.UnitTests.Configuration
{
    public enum MessageFormat
    {
        Binary,
        Avro,
        Json
    }

    [Serializable]
    public class BusSettings: IConfigObject
    {        
        [Required]
        public string TopicName { get; set; }
       
        public MessageFormat MessageFormat { get; set; }

        public MessageFormat? MessageFormatNullable { get; set; }

        /// <summary> Request timeout in ms</summary>
        public int RequestTimeoutInMs { get; set; }

        public int? RequestTimeoutInMsNullable { get; set; }

        public ConsumerSettingsObj ConsumerSettings { get; set; }

        public TimeSpan TimeSpan { get; set; }

        public Regex Regex { get; set; }

        public Uri Uri { get; set; }

        public DateTime DateTime { get; set; }

        public DateTime Date { get; set; }

        public DateTime? DateTimeNullable { get; set; }
    }



    public class Country : IConfigObject
    {
        public string CountryCode { get; set; }
    }

    public class ConsumerSettingsObj : IConfigObject
    {
        [Required]
        public string ConsumerName { get; set; } = "Default";

        public string ConsumerId { get; set; }

        public Country Country { get; set; }
    }

    public class FirstLevel : IConfigObject
    {
        public Level2 NextLevel { get; set; }
    }

    public class Level2 : IConfigObject
    {
        public Level3 NextLevel { get; set; }
    }

    public class Level3 : IConfigObject
    {
        [Required]
        public string ID { get; set; }
        public string Name { get; set; }
    }

   
    [ConfigurationRoot("Prefix1.Prefix2", RootStrategy.AppendClassNameToPath)]
    public class GatorConfig : IConfigObject
    {
        public string Name { get; set; } = "Default";
    }

    [ConfigurationRoot("Prefix1.Prefix2.gatorConfig", RootStrategy.ReplaceClassNameWithPath)]
    public class GatorLowerCase : IConfigObject
    {
        public string Name { get; set; }

        public NotConfigObject NotConfigObject { get; set; }
    }


    [ConfigurationRoot("Prefix1.Prefix2.GatorConfig", RootStrategy.ReplaceClassNameWithPath)]
    public class Gator : IConfigObject
    {
        public string Name { get; set; }

        public NotConfigObject NotConfigObject { get; set; }
    }



    public class NotConfigObject
    {
        public string Name { get; set; }
    }
}