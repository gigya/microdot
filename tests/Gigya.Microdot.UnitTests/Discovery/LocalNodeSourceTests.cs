using System;
using System.Linq;
using System.Net;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceDiscovery;
using Gigya.Microdot.SharedLogic.Rewrite;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Discovery
{
    public class LocalNodeSourceTests
    {
        private TestingKernel<ConsoleLog> _kernel;
        private INodeSource _localNodeSource;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _kernel = new TestingKernel<ConsoleLog>();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _kernel.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            var deployment = new DeploymentIdentifier("MyService", "prod");

            var sources = _kernel.Get<Func<DeploymentIdentifier,INodeSource[]>>()(deployment);
            _localNodeSource = sources.Single(x => x.Type == "Local");
        }

        [Test]
        public void OneSingleLocalHostNode()
        {
            var node = _localNodeSource.GetNodes().Single();
            node.Hostname.ShouldBe(Dns.GetHostName());
            node.Port.ShouldBeNull();
        }

        [Test]
        public void NotSupportsMultipleEnvironments()
        {
            _localNodeSource.SupportsMultipleEnvironments.ShouldBeFalse();
        }

        [Test]
        public void IsNeverUndeployed()
        {
            _localNodeSource.WasUndeployed.ShouldBeFalse();
        }

    }
}
