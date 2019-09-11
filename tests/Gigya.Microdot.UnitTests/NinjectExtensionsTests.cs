using System;
using System.Linq;

using Gigya.Microdot.Ninject;

using Ninject;

using NUnit.Framework;

using Shouldly;

namespace Gigya.Microdot.UnitTests
{
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class NinjectExtensionsTests
    {
        [Test]
        public void BindPerKey_ServiceToImplementation_ShouldSucceed()
        {
            Test_BindPerKey<IFoo, Foo>();
        }

        [Test]
        public void BindPerKey_SelfBinding_ShouldSucceed()
        {
            Test_BindPerKey<Foo, Foo>();
        }

        [Test]
        public void BindPerKey_ServiceToService_ShouldFail()
        {
            Should.Throw<ActivationException>(() => Test_BindPerKey<IFoo, IFoo>());
        }

        [Test]
        public void BindPerKeyWithParam_ServiceToImplementation_ShouldSucceed()
        {
            Test_BindPerKeyWithParam<IFoo, Foo>();
        }

        [Test]
        public void BindPerKeyWithParam_SelfBinding_ShouldSucceed()
        {
            Test_BindPerKeyWithParam<Foo, Foo>();
        }

        [Test]
        public void BindPerKeyWithParam_ServiceToService_ShouldFail()
        {
            Should.Throw<ActivationException>(() => Test_BindPerKeyWithParam<IFoo, IFoo>());
        }

        [Test]
        public void BindPerMultiKey_ServiceToImplementation_ShouldSucceed()
        {
            Test_BindPerMultiKey<IFoo, Foo>();
        }

        [Test]
        public void BindPerMultiKey_SelfBinding_ShouldSucceed()
        {
            Test_BindPerMultiKey<Foo, Foo>();
        }

        [Test]
        public void BindPerMultiKey_ServiceToService_ShouldFail()
        {
            Should.Throw<ActivationException>(() => Test_BindPerMultiKey<IFoo, IFoo>());
        }

        [Test]
        public void BindPerKey_WithAndWithoutParam_DifferentInstances()
        {
            var k = new StandardKernel();

            k.BindPerKey<int, IFoo, Foo>();
            k.BindPerKey<int, string, IFoo, Foo>();

            var factoryA = k.Get<Func<int, IFoo>>();
            var factoryB = k.Get<Func<int, string, IFoo>>();

            factoryA(1).ShouldNotBeSameAs(factoryB(1, "red"));
        }


        private void Test_BindPerKey<TService, TImplementation>() where TImplementation : TService
        {
            var k = new StandardKernel();
            
            k.BindPerKey<int, TService, TImplementation>();

            k.Get<TService>().ShouldBeOfType<TImplementation>();

            Func<Func<int, TService>> metafactory = () => k.Get<Func<int, TService>>();
            var factory = metafactory();
            factory(1).ShouldBeSameAs(factory(1));
            factory(1).ShouldNotBeSameAs(factory(2));

            metafactory()(1).ShouldBeSameAs(metafactory()(1));
            metafactory()(1).ShouldNotBeSameAs(metafactory()(2));
        }

        private void Test_BindPerKeyWithParam<TService, TImplementation>() where TImplementation : TService
        {
            var k = new StandardKernel();

            k.BindPerKey<int, string, TService, TImplementation>();

            k.Get<TService>().ShouldBeOfType<TImplementation>();

            Func<Func<int, string, TService>> metafactory = () => k.Get<Func<int, string, TService>>();

            var factory = metafactory();
            var blue = factory(1, "blue");
            new[] { factory(1, "red"), factory(1, "green"), factory(1, "blue") }
                .Distinct()
                .Single()
                .ShouldBeSameAs(blue);

            blue = metafactory()(1, "blue");
            new[] { metafactory()(1, "red"), metafactory()(1, "green"), metafactory()(1, "blue") }
                .Distinct()
                .Single()
                .ShouldBeSameAs(blue);
        }

        private void Test_BindPerMultiKey<TService, TImplementation>() where TImplementation : TService
        {
            var k = new StandardKernel();

            k.BindPerMultiKey<int, string, TService, TImplementation>();

            k.Get<TService>().ShouldBeOfType<TImplementation>();

            Func<Func<int, string, TService>> metafactory = () => k.Get<Func<int, string, TService>>();

            var factory = metafactory();
            var blue = factory(1, "blue");
            factory(1, "blue").ShouldBeSameAs(blue);
            new[] { factory(1, "red"), factory(1, "green"), factory(1, "blue") }
                .Distinct()
                .Count()
                .ShouldBe(3);

            blue = metafactory()(1, "blue");
            metafactory()(1, "blue").ShouldBeSameAs(blue);
            new[] { metafactory()(1, "red"), metafactory()(1, "green"), metafactory()(1, "blue") }
                .Distinct()
                .Count()
                .ShouldBe(3);
        }
    }

    public interface IFoo { }
    public class Foo : IFoo { }
}
