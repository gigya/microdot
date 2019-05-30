using System;
using FluentAssertions;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration
{
    [TestFixture]
    public class ConfigObjectsCacheTests
    {
        [Test]
        public void Register2Creators()
        {
            IConfigObjectCreator config1 = Substitute.For<IConfigObjectCreator>();
            IConfigObjectCreator config2 = Substitute.For<IConfigObjectCreator>();

            ConfigObjectsCache cache = new ConfigObjectsCache(new ConfigDecryptor());
            cache.RegisterConfigObjectCreator(config1);
            cache.RegisterConfigObjectCreator(config2);

            cache.DecryptAndReloadConfigObjects(null, null);

            config1.Received(1).Reload();
            config2.Received(1).Reload();
        }
    }
}
