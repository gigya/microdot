using System.Threading.Tasks;
using FluentAssertions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NSubstitute;

using NUnit.Framework;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class NinjectTest
    {
        [Test]
        public async Task CanReplaceServiceProxy()
        {
            var kernel = new TestingKernel<ConsoleLog>(k =>
                                                           {
                                                               var demoService = Substitute.For<IDemoService>();
                                                               demoService.IncrementInt(Arg.Any<int>()).Returns(100);
                                                               k.Rebind<IDemoService>().ToConstant(demoService);
                                                               var serviceProxy = Substitute.For<IServiceProxyProvider<IDemoService>>();
                                                               serviceProxy.Client.Returns(demoService);
                                                               k.Rebind<IServiceProxyProvider<IDemoService>>().ToConstant(serviceProxy);
                                                           });
            var useServiceWithNoCache = kernel.Get<UseServiceWithNoCache>();
            (await useServiceWithNoCache.DemoService.IncrementInt(1)).Should().Be(100);
        }
    }

    public class UseServiceWithNoCache
    {
        public IDemoService DemoService { get; }

        public UseServiceWithNoCache(IServiceProxyProvider<IDemoService> demoService)
        {
            DemoService = demoService.Client;
        }
    }
}